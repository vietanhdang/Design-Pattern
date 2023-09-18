using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Windows.Forms;

namespace WindowsFormsApp1.Models
{
    public class EInvoiceCrawler
    {
        /// <summary>
        /// Địa chỉ API hóa đơn điện tử
        /// </summary>
        private readonly static string EInvoice_API = "https://hoadondientu.gdt.gov.vn:30000";

        /// <summary>
        /// Mã số thuế của doanh nghiệp
        /// </summary>
        private string _taxcode { get; set; }

        /// <summary>
        /// Tên đăng nhập
        /// </summary>
        private string _username { get; set; }

        /// <summary>
        /// Mật khẩu
        /// </summary>
        private string _password { get; set; }

        /// <summary>
        /// Token xác thực
        /// </summary>
        private string _token { get; set; }

        /// <summary>
        /// Lưu trữ cookie
        /// </summary>
        private CookieContainer _cookieContainer { get; set; }

        /// <summary>
        /// HTTP Client
        /// </summary>
        private HttpClient _client { get; set; }

        /// <summary>
        /// Quản lý các yêu cầu HTTP
        /// </summary>
        private HttpClientHandler _handler { get; set; }
        public EInvoiceCrawler(string taxcode, string username, string password)
        {
            _taxcode = taxcode;
            _username = username;
            _password = password;
        }
        public EInvoiceCrawler()
        {

        }

        /// <summary>
        /// Check token theo mst xem có còn thời hạn không
        /// </summary>
        public async Task CheckToken()
        {
            try
            {
                string[] files = Directory.GetFiles(Application.StartupPath, $"{_taxcode}_Token_*.txt");
                bool forceGetToken = true; // biến này dùng để check xem có lấy token mới không
                // nếu tìm thấy token thì check expired timestamp
                if (files.Length > 0)
                {
                    string tokenPath = files[0];
                    string fileName = Path.GetFileNameWithoutExtension(tokenPath);
                    string[] fileNameParts = fileName.Split('_');
                    string expirationUnixTimeStr = fileNameParts[fileNameParts.Length - 1];
                    var expirationUnixTime = 0;
                    int.TryParse(expirationUnixTimeStr, out expirationUnixTime);
                    var unixTimestamp = (int)DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
                    if (unixTimestamp < expirationUnixTime)
                    {
                        _token = File.ReadAllText(tokenPath);
                        forceGetToken = false;
                    }
                    else
                    {
                        File.Delete(tokenPath);
                    }
                }

                if (forceGetToken)
                {
                    // nếu không tìm thấy token thì lấy token theo username và password
                    var loginResponse = await LogInAsync();
                    if (loginResponse != null && loginResponse.Token != null)
                    {
                        _token = loginResponse.Token;
                        var unixTimestamp = (int)DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
                        // cộng thêm 12 tiếng để lấy thời gian hết hạn
                        var expirationUnixTime = unixTimestamp + 12 * 60 * 60;
                        string tokenPath = Path.Combine(Application.StartupPath, $"{_taxcode}_Token_{expirationUnixTime}.txt");
                        File.WriteAllText(Application.StartupPath + $"\\{_taxcode}_Token_{expirationUnixTime}.txt", loginResponse.Token);
                    }
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        /// <summary>
        /// Hàm này dùng để reset lại header mỗi khi send request
        /// </summary>
        private void ResetHeaderContent()
        {
            _handler?.Dispose();
            _client?.Dispose();
            _cookieContainer = new CookieContainer();
            _handler = new HttpClientHandler()
            {
                CookieContainer = _cookieContainer,
            };
            _client = new HttpClient(_handler) { BaseAddress = new Uri(EInvoice_API) };
            _client.DefaultRequestHeaders.Clear();
            _client.Timeout = TimeSpan.FromSeconds(30); // thời gian timeout tối đa là 30s
            _client.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate, br");
            _client.DefaultRequestHeaders.Add("Accept-Language", "vi-VN,vi;q=0.9,fr-FR;q=0.8,fr;q=0.7,en-US;q=0.6,en;q=0.5");
            _client.DefaultRequestHeaders.Add("Connection", "keep-alive");
            _client.DefaultRequestHeaders.Add("Host", "hoadondientu.gdt.gov.vn:30000");
            _client.DefaultRequestHeaders.Add("sec-ch-ua", "\"Google Chrome\";v=\"105\", \"Not) A; Brand\"; v=\"8\", \"Chromium\"; v=\"105\"");
            _client.DefaultRequestHeaders.Add("sec-ch-ua-mobile", "20");
            _client.DefaultRequestHeaders.Add("sec-ch-ua-platform", RandomHeaderRequest.GetRandomSecChUaPlatform());
            _client.DefaultRequestHeaders.Add("User-Agent", RandomHeaderRequest.GetRandomUserAgent());
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _token);
        }

        /// <summary>
        /// Đăng nhập vào trang hóa đơn điện tử để lấy token
        /// </summary>
        /// <param name="captcha">Captcha dùng để đăng nhập</param>
        /// <returns></returns>
        public async Task<LogInResponse> LogInAsync()
        {
            ResetHeaderContent();
            try
            {
                // lấy captcha từ website hóa đơn điện tử
                var captcha = await GetCaptchaSvgFromWebsiteHDDT();

                // build request đăng nhập
                var request = new Dictionary<string, string>
                {
                    { "ckey", captcha.Key },
                    { "cvalue", captcha.Content },
                    { "username", _username },
                    { "password", _password }
                };

                // gửi request đăng nhập
                var byteContent = new ByteArrayContent(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(request)));
                byteContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                HttpResponseMessage responseMessage = await _client.PostAsync($"{DownloadEinvoiceSetting.LinkLogIn}", byteContent);
                var stringContent = await responseMessage.Content.ReadAsStringAsync();
                var loginResponse = JsonConvert.DeserializeObject<LogInResponse>(stringContent);
                if (!responseMessage.IsSuccessStatusCode)
                {
                    if (!string.IsNullOrEmpty(stringContent))
                    {
                        var errorMessage = JsonConvert.DeserializeObject<BaseTaxaxionResponse>(stringContent);
                        if (!string.IsNullOrEmpty(errorMessage.Message))
                        {
                            string messageLower = errorMessage.Message.Trim().ToLower();
                            if (messageLower.Contains("mã captcha không đúng"))
                            {
                                // TODO: Xử lý khi phản hồi trả về là mã captcha không đúng
                            }
                            else if (messageLower.Contains("tên đăng nhập hoặc mật khẩu không đúng"))
                            {
                                // TODO: Xử lý khi phản hồi trả về là tên đăng nhập hoặc mật khẩu không đúng
                            }
                        }
                    }
                }
                // những lỗi khác mà chưa case ở trên thì cứ EnsureSuccessStatusCode để nó throw exception ra nếu có lỗi
                responseMessage.EnsureSuccessStatusCode();
                return loginResponse;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        /// <summary>
        /// Lấy mã captcha từ website hóa đơn điện tử
        /// </summary>
        /// <returns></returns>
        public async Task<CaptchaResponse> GetCaptchaSvgFromWebsiteHDDT()
        {
            ResetHeaderContent();
            var response = await _client.GetAsync("/captcha");
            response.EnsureSuccessStatusCode();
            string res = await response.Content.ReadAsStringAsync();
            var captcha = JsonConvert.DeserializeObject<CaptchaResponse>(res);
            MisaDecaptcha misaDecaptcha = new MisaDecaptcha();
            byte[] jpegBytes = misaDecaptcha.ConvertSvgToJpegByte(captcha.Content);
            var decaptcha = await misaDecaptcha.DecodeCaptchaWithApi(jpegBytes);
            captcha.Content = decaptcha.CaptchaText;
            return captcha;
        }

        public string BuildQueryParam(string url, Dictionary<string, string> queryParams)
        {
            string queryString = string.Join("&", queryParams.Select(kvp => $"{kvp.Key}={kvp.Value}"));
            return url + "?" + queryString;
        }

        /// <summary>
        /// Lấy chi tiết hóa đơn
        /// </summary>
        /// <param name="requests"></param>
        /// <param name="progressBarManager"></param>
        /// <returns></returns>
        public async Task<List<HoaDon>> GetInvoiceInfoDetailAsyc(List<InvoicesResponse> requests, string linkGetDetail)
        {
            ResetHeaderContent();
            List<HoaDon> invoiceDetails = new List<HoaDon>();
            List<HoaDon> tempInvoiceDetails = new List<HoaDon>();
            List<HoaDon> invoiceDetailsException = new List<HoaDon>();
            requests.ForEach(x =>
            {
                tempInvoiceDetails.AddRange(x.Datas);
            });

            for (int i = 0; i < tempInvoiceDetails.Count(); i++)
            {
                var queryParams = new Dictionary<string, string> {
                    { "nbmst", tempInvoiceDetails[i].MSTNBan },
                    { "khhdon", tempInvoiceDetails[i].KyHieuHDon },
                    { "shdon", tempInvoiceDetails[i].SoHDon },
                    { "khmshdon", tempInvoiceDetails[i].KyHieuMSoHDon.ToString()}
                };

                try
                {
                    // build param
                    string queryString = string.Join("&", queryParams.Select(kvp => $"{kvp.Key}={kvp.Value}"));
                    string urlWithParams = linkGetDetail + "?" + queryString;
                    var response = await CommonPattern.RetryOperationAsync(async () =>
                    {
                        //return await _client.GetAsync(urlWithParams);
                        var httpResponse = await _client.GetAsync(urlWithParams);
                        if (!httpResponse.IsSuccessStatusCode)
                        {
                            Logger.Log($"Failed - {JsonConvert.SerializeObject(queryParams)}");
                            throw new Exception($"HTTP request failed with status code {httpResponse.StatusCode}");
                        }
                        return httpResponse;
                    });
                    response.EnsureSuccessStatusCode();
                    string stringContent = await response.Content.ReadAsStringAsync();
                    invoiceDetails.Add(JsonConvert.DeserializeObject<HoaDon>(stringContent));
                    Logger.Log($"Success - {JsonConvert.SerializeObject(queryParams)}");
                }
                catch (Exception ex)
                {
                    Logger.Log($"Failed - {JsonConvert.SerializeObject(queryParams)} - {ex.Message}");
                }
            }

            return invoiceDetails;
        }

        //private async Task<List<InvoicesResponse>> FetchInvoiceListAsync(string baseUrl, InvoiceInfoRequest request, DateTime currentDate, DateTime? endDate = null)
        //{
        //    ResetHeaderContent();
        //    List<InvoicesResponse> invoicesInRange = new List<InvoicesResponse>();

        //    // Tạo biểu thức tìm kiếm cho ngày hiện tại
        //    var textSearch = $"tdlap=ge={currentDate:dd/MM/yyyyT00:00:00};tdlap=le={currentDate:dd/MM/yyyyT23:59:59}";

        //    // nếu có endDate thì build lại query
        //    // (mục đích chính là test case nếu có hóa đơn khởi tạo từ máy tính tiền thì kiểm tra xem có không)
        //    if (endDate != null)
        //    {
        //        textSearch = $"tdlap=ge={currentDate:dd/MM/yyyyT00:00:00};tdlap=le={endDate:dd/MM/yyyyT23:59:59}";
        //    }

        //    // Trạng thái hóa đơn
        //    if (request.InvoiceStatus != 0)
        //    {
        //        textSearch += $";tthai=={request.InvoiceStatus}";
        //    }

        //    // Kết quả kiểm tra hóa đơn
        //    if (request.InvoiceCheckResult != -1)
        //    {
        //        // nếu khác tất cả
        //        textSearch += $";ttxly=={request.InvoiceCheckResult}";
        //    }
        //    else
        //    {
        //        // Nếu khác tất cả và là hóa đơn mua vào thì lấy cả 3 trạng thái
        //        // 5: "Đã cấp mã hóa đơn",
        //        // 6: "Tổng cục thuế đã nhận không mã",
        //        // 8: "Tổng cục thuế đã nhận hóa đơn có mã khởi tạo từ máy tính tiền"
        //        if (request.EinvoiceTab == 1)
        //        {
        //            textSearch += $";ttxly=in=(5,6,8)";
        //        }
        //    }

        //    if (request.Unhiem && (baseUrl == DownloadEinvoiceSetting.LinkSoldInvoices || baseUrl == DownloadEinvoiceSetting.LinkPurchaseInvoices))
        //    {
        //        textSearch += $";unhiem==1";
        //    }

        //    var queryParams = new Dictionary<string, string>
        //    {
        //        { "sort", "tdlap:desc,khmshdon:asc,shdon:desc" },
        //        { "size", $"{request.Take}" },
        //        { "search", textSearch }
        //    };

        //    // Build parameter
        //    string urlWithParams = BuildQueryParam(baseUrl, queryParams);

        //    // check xem có trang tiếp theo hay không
        //    bool hasNextState = true;
        //    // state của kết quả trả về hiện tại là param của trang sau
        //    string currentState = null;

        //    while (hasNextState)
        //    {
        //        // build lại param lấy trang tiếp theo dựa trên state
        //        if (!string.IsNullOrEmpty(currentState))
        //        {
        //            queryParams["state"] = currentState;
        //            urlWithParams = BuildQueryParam(baseUrl, queryParams);
        //        }

        //        try
        //        {
        //            var stringContent = "";
        //            var response = CommonPattern.RetryOperationAsync(async () =>
        //            {
        //                var httpResponse = await _client.GetAsync(urlWithParams);
        //                stringContent = await httpResponse.Content.ReadAsStringAsync();
        //                httpResponse.EnsureSuccessStatusCode();
        //                if (!httpResponse.IsSuccessStatusCode)
        //                {
        //                    throw new Exception($"FetchInvoiceListAsync_Request failed with status code {httpResponse.StatusCode} - Message: {stringContent} - {JsonConvert.SerializeObject(queryParams)}");
        //                }
        //                return httpResponse;
        //            });

        //            var invoiceResponse = JsonConvert.DeserializeObject<InvoicesResponse>(stringContent);
        //            currentState = invoiceResponse.State;

        //            // Thêm hóa đơn vào danh sách
        //            invoicesInRange.Add(invoiceResponse);

        //            hasNextState = !string.IsNullOrEmpty(currentState);
        //        }
        //        catch (Exception ex)
        //        {
        //            hasNextState = false;
        //            Logger.Log(ex.Message);
        //            throw ex;
        //        }
        //    }

        //    return invoicesInRange;
        //}

        ///// <summary>
        ///// Tải XML
        ///// </summary>
        ///// <param name="requests"></param>
        ///// <param name="progressBarManager"></param>
        ///// <returns></returns>
        //public async Task<bool> DownloadXmlAsync(Dictionary<DateTime, List<HoaDon>> requests)
        //{
        //    List<byte[]> downloadXMLResults = new List<byte[]>();
        //    foreach (var date in requests)
        //    {
        //        int totalSuccess = 0;
        //        int totalError = 0;
        //        for (int i = 0; i < date.Value.Count(); i++)
        //        {
        //            try
        //            {
        //                var queryParams = new Dictionary<string, string> {
        //                    { "nbmst", date.Value[i].MSTNBan }, // MSTNBan
        //                    { "khhdon", date.Value[i].KyHieuHDon }, // Series
        //                    { "shdon", date.Value[i].SoHDon }, // InvoiceNo
        //                    { "khmshdon", date.Value[i].KyHieuMSoHDon.ToString() } // TemplateNo
        //                };

        //                // check xem nếu trong ký hiệu hóa đơn ký tự thứ 3 là M thì là link khởi tạo từ máy tính tiền
        //                var linkExportXml = date.Value[i].KyHieuHDon.IndexOf("M") == 3 ? DownloadEinvoiceSetting.LinkExportXmlFromCashRegister : DownloadEinvoiceSetting.LinkExportXml;

        //                // build query param
        //                string queryString = string.Join("&", queryParams.Select(kvp => $"{kvp.Key}={kvp.Value}"));
        //                string urlWithParams = linkExportXml + "?" + queryString;

        //                // tải xml, nếu sau 3 lần tải thất bại sẽ trả về exception do lỗi
        //                var response = await CommonPattern.RetryOperationAsync(async () =>
        //                {
        //                    // reset lại header mới cho mỗi lần tải
        //                    ResetHeaderContent();
        //                    _client.Timeout = TimeSpan.FromSeconds(2); // thời gian timeout tối đa là 2s

        //                    var httpResponse = await _client.GetAsync(urlWithParams);
        //                    if (!httpResponse.IsSuccessStatusCode)
        //                    {
        //                        string errorContent = httpResponse.Content.ReadAsStringAsync().Result;
        //                        if (!string.IsNullOrEmpty(errorContent))
        //                        {
        //                            var errorMessage = JsonConvert.DeserializeObject<BaseTaxaxionResponse>(errorContent);
        //                            if (!string.IsNullOrEmpty(errorMessage.Message))
        //                            {
        //                                totalError++;
        //                                // break retry luôn nếu không tìm thấy thông tin hóa đơn
        //                                throw new DownloadXMLException($"DownloadXmlAsync_Response from HDDT : {errorMessage.Message} - {JsonConvert.SerializeObject(queryParams)}");
        //                            }
        //                        }
        //                        throw new Exception($"DownloadXmlAsync_Request failed with status code {httpResponse.StatusCode} - {JsonConvert.SerializeObject(queryParams)}");
        //                    }
        //                    return httpResponse;
        //                });

        //                new Task(async () =>
        //                {
        //                    // nếu response thành công thì sẽ đọc xml trong zip
        //                    byte[] responseBodyBytes = response.Content.ReadAsByteArrayAsync().Result;
        //                    await ExtractAndSendXmlAsync(responseBodyBytes, date.Value[i]);
        //                }).Start();

        //                totalSuccess++;

        //                // TODO: Gửi hóa đơn sang inbot
        //                // tải 10 cái delay 0.5s
        //                if ((i + 1) % 5 == 0)
        //                {
        //                    Task.Delay(500).Wait();
        //                }
        //            }
        //            catch (Exception ex)
        //            {
        //                totalError++;
        //                Logger.Log($"DownloadXmlAsync_{ex.Message}");
        //            }
        //        }

        //        Logger.Log($"Hoàn thành tải thành công {totalSuccess}/{date.Value.Count()} - Thất bại: {totalError}");
        //    }

        //    return true;
        //}
        ///// <summary>
        ///// Giải nén folder và tìm ra file xml để gửi sang inbot
        ///// </summary>
        ///// <param name="zipData"></param>
        ///// <param name="request"></param>
        ///// <returns></returns>
        //private async Task<bool> ExtractAndSendXmlAsync(byte[] zipData, HoaDon request)
        //{
        //    try
        //    {
        //        using (var zipStream = new MemoryStream(zipData))
        //        using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Read))
        //        {
        //            // save xml and html
        //            foreach (var entry in archive.Entries)
        //            {
        //                var extension = Path.GetExtension(entry.Name);
        //                var fileName = Path.GetFileName(entry.Name);
        //                // lưu cả file xml và html
        //                if (extension == ".html" || extension == ".xml")
        //                {
        //                    using (var xmlStream = entry.Open())
        //                    using (var xmlMemoryStream = new MemoryStream())
        //                    {
        //                        await xmlStream.CopyToAsync(xmlMemoryStream);
        //                        byte[] xmlBytes = xmlMemoryStream.ToArray();

        //                        // test lưu xem tải về đủ chưa
        //                        string folderPath = $"xml/{request.MSTNBan}";
        //                        if (!Directory.Exists(folderPath))
        //                        {
        //                            Directory.CreateDirectory(folderPath);
        //                        }
        //                        string folderName = $"{folderPath}/{request.KyHieuMSoHDon}_{request.KyHieuHDon}_{request.SoHDon}_{fileName}";
        //                        File.WriteAllBytes(folderName, xmlBytes);
        //                        Logger.Log($"Download {folderName} success");
        //                    }
        //                }
        //            }
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        Logger.Log(ex.Message);
        //        throw ex;
        //    }
        //    return true;
        //}

        public async Task<string> ViewDetailHTML(TaxaxionRequest request)
        {
            var queryParams = new Dictionary<string, string> {
                            { "nbmst", request.MSTNBan }, // MSTNBan
                            { "khhdon", request.Series }, // Series
                            { "shdon", request.InvoiceNo }, // InvoiceNo
                            { "khmshdon", request.TemplateNo.ToString() } // TemplateNo
                        };

            // check xem nếu trong ký hiệu hóa đơn ký tự thứ 3 là M thì là link khởi tạo từ máy tính tiền
            var linkExportXml = request.Series.IndexOf("M") == 3 ? DownloadEinvoiceSetting.LinkExportXmlFromCashRegister : DownloadEinvoiceSetting.LinkExportXml;

            // build query param
            string queryString = string.Join("&", queryParams.Select(kvp => $"{kvp.Key}={kvp.Value}"));
            string urlWithParams = linkExportXml + "?" + queryString;

            // tải xml, nếu sau 3 lần tải thất bại sẽ trả về exception do lỗi
            var response = await CommonPattern.RetryOperationAsync(async () =>
            {
                // reset lại header mới cho mỗi lần tải
                ResetHeaderContent();
                _client.Timeout = TimeSpan.FromSeconds(2); // thời gian timeout tối đa là 2s

                var httpResponse = await _client.GetAsync(urlWithParams);
                if (!httpResponse.IsSuccessStatusCode)
                {
                    string errorContent = await httpResponse.Content.ReadAsStringAsync();
                    if (!string.IsNullOrEmpty(errorContent))
                    {
                        var errorMessage = JsonConvert.DeserializeObject<BaseTaxaxionResponse>(errorContent);
                        if (!string.IsNullOrEmpty(errorMessage.Message))
                        {
                            // break retry luôn nếu không tìm thấy thông tin hóa đơn
                            throw new DownloadXMLException($"DownloadXmlAsync_Response from HDDT : {errorMessage.Message} - {JsonConvert.SerializeObject(queryParams)}");
                        }
                    }
                    throw new Exception($"DownloadXmlAsync_Request failed with status code {httpResponse.StatusCode} - {JsonConvert.SerializeObject(queryParams)}");
                }
                return httpResponse;
            });

            byte[] responseBodyBytes = response.Content.ReadAsByteArrayAsync().Result;
            return ReadHtmlFromZip(responseBodyBytes);
        }

        private string ReadHtmlFromZip(byte[] zipData)
        {
            try
            {
                using (MemoryStream zipStream = new MemoryStream(zipData))
                using (ZipArchive archive = new ZipArchive(zipStream, ZipArchiveMode.Read))
                {
                    foreach (ZipArchiveEntry entry in archive.Entries)
                    {
                        if (entry.FullName.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
                        {
                            // Tìm thấy tệp HTML trong ZIP, đọc nội dung của nó
                            using (Stream htmlStream = entry.Open())
                            using (StreamReader reader = new StreamReader(htmlStream))
                            {
                                string htmlContent = reader.ReadToEnd();
                                // Ở đây bạn có thể xử lý nội dung HTML theo ý muốn
                                // Ví dụ: hiển thị nó trên một TextBox
                                return htmlContent;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi khi đọc ZIP: " + ex.Message);
            }
            return "";
        }

        /// <summary>
        /// Đồng bộ hóa đơn trong khoảng thời gian
        /// </summary>
        /// <param name="request"></param>
        /// <param name="progressBarManager"></param>
        /// <returns></returns>
        //public Dictionary<DateTime, List<HoaDon>> GetInvoicesInRange(InvoiceInfoRequest request, ProgressBarManager progressBarManager)
        //{
        //    ResetHeaderContent();

        //    string linkInvoice = $"{DownloadEinvoiceSetting.LinkPurchaseInvoices}";
        //    string linkInvoiceFromCashRegister = $"{DownloadEinvoiceSetting.LinkPurchaseInvoicesFromCashRegister}";

        //    if (request.EinvoiceTab == 0)
        //    {
        //        linkInvoice = $"{DownloadEinvoiceSetting.LinkSoldInvoices}";
        //        linkInvoiceFromCashRegister = $"{DownloadEinvoiceSetting.LinkSoldInvoicesFromCashRegister}";
        //    }

        //    // tổng danh sách hóa đơn sau khi thành công theo ngày
        //    var invoicesInRange = new Dictionary<DateTime, List<HoaDon>>();

        //    List<InvoicesResponse> invoices = null;
        //    List<InvoicesResponse> invoicesFromCashRegister = null;

        //    int completed = 0;
        //    int total = request.EndDate.Date.Subtract(request.StartDate.Date).Days + 1;
        //    bool hasInvoiceFromCashRegister = false;

        //    DateTime currentDate = request.StartDate.Date;
        //    DateTime endDate = request.EndDate.Date;

        //    // KH ít sử dụng hóa đơn từ máy tính tiền nên take 1 để check xem có total bao nhiêu hóa đơn
        //    // nếu không có thì thôi đỡ tốn gọi 2 api bên dưới
        //    invoicesFromCashRegister = CommonPattern.RetryOperation(() =>
        //    {
        //        request.Take = 1;
        //        return FetchInvoiceListAsync(linkInvoiceFromCashRegister, request, currentDate, endDate);
        //    });

        //    if (invoicesFromCashRegister != null && invoicesFromCashRegister.Count > 0 && invoicesFromCashRegister.FirstOrDefault().Total > 0)
        //    {
        //        hasInvoiceFromCashRegister = true;
        //    }

        //    request.Take = 50;
        //    while (currentDate <= request.EndDate.Date)
        //    {
        //        try
        //        {
        //            var lstXMLDownload = new List<HoaDon>();

        //            // Lấy Hóa đơn điện tử
        //            invoices = CommonPattern.RetryOperation(() =>
        //            {
        //                return FetchInvoiceListAsync(linkInvoice, request, currentDate);
        //            });

        //            if (invoices != null && invoices.Count > 0)
        //            {
        //                invoices.ForEach(x =>
        //                {
        //                    lstXMLDownload.AddRange(x.Datas);
        //                });
        //            }

        //            // Lấy Hóa đơn có mã khởi tạo từ máy tính tiền
        //            if (!request.Unhiem && hasInvoiceFromCashRegister)
        //            {
        //                invoicesFromCashRegister = CommonPattern.RetryOperation(() =>
        //                {
        //                    return FetchInvoiceListAsync(linkInvoiceFromCashRegister, request, currentDate);
        //                });

        //                if (invoicesFromCashRegister != null && invoicesFromCashRegister.Count > 0)
        //                {
        //                    invoicesFromCashRegister.ForEach(x =>
        //                    {
        //                        lstXMLDownload.AddRange(x.Datas);
        //                    });
        //                }
        //            }

        //            var invoiceBotMapping = MappingTaxaxionToInbot(lstXMLDownload);
        //            var subId = "asp-c7f68009dd07ab2b8b6db026";
        //            var orgId = "639ac3a8416ebb5fcdd23332";
        //            var taxAuthorityResponse = new TaxAuthoritySyncMaster
        //            {
        //                TotalCrawl = invoiceBotMapping.Count,
        //                TotalResponse = invoices.FirstOrDefault().Total.Value + invoicesFromCashRegister.FirstOrDefault().Total.Value,
        //                DateConnect = currentDate,
        //                SubscriberId = subId,
        //                OrganizationId = orgId,
        //                Id = BuildSyncHistoryMasterId(subId, orgId, currentDate),
        //                Invoices = invoiceBotMapping
        //            };

        //            // TODO: Call về Kế toán Lưu vào bảng inb_bot_list và 
        //            Logger.Log($"Hoàn thành lấy danh sách hóa đơn ngày {currentDate} - Total: {lstXMLDownload.Count} - Total In HDDT: {invoices.FirstOrDefault().Total.Value + invoicesFromCashRegister.FirstOrDefault().Total.Value}");

        //            invoicesInRange.Add(currentDate, lstXMLDownload);

        //            //// DOWNLOAD XML bất đồng bộ
        //            //var invoiceListDownload = new Dictionary<DateTime, List<HoaDon>>
        //            //{
        //            //    { currentDate, lstXMLDownload }
        //            //};

        //            //if (invoiceListDownload.Count > 0)
        //            //{
        //            //    DownloadXml(invoiceListDownload);
        //            //}
        //        }
        //        finally
        //        {
        //            completed++;
        //            progressBarManager.UpdateProgress(completed, total);
        //            // Di chuyển đến ngày tiếp theo
        //            currentDate = currentDate.AddDays(1);
        //        }
        //    }



        //    return invoicesInRange;
        //}
        /// <summary>
        /// Lấy thông tin hóa đơn khi không kết nối
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        private async Task<HoaDon> GetInvoiceNoAuthorizationAsync(TaxaxionRequest request)
        {
            Dictionary<string, string> queryParams = new Dictionary<string, string>
            {
                { "khmshdon", request.TemplateNo },
                { "nbmst", request.MSTNBan },
                { "shdon", request.InvoiceNo },
                { "cvalue", request.CaptchaText },
                { "ckey", request.CaptchaKey}
            };
            var linkSearch = DownloadEinvoiceSetting.LinkSearch;

            if (!request.Series.StartsWith("C") && !request.Series.StartsWith("K"))
            {
                request.Series = request.Series.Substring(1);
            }
            if (request.Series.IndexOf("M") == 3)
            {
                linkSearch = DownloadEinvoiceSetting.LinkSearchFromCashRegister;
            }
            if (request.TemplateNo == "6")
            {
                queryParams.Add("hdon", $"06_0{(request.Series.IndexOf("N") == 3 ? 1 : 2)}");
                queryParams.Add("tdlap", request.InvoiceDate.Value.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"));
            }
            else
            {
                queryParams.Add("hdon", $"0{request.TemplateNo}");
                queryParams.Add("tgtttbso", request.Amount.ToString());
            }

            queryParams.Add("khhdon", request.Series);



            // Build the query string
            string queryString = string.Join("&", queryParams.Select(kvp => $"{kvp.Key}={kvp.Value}"));
            string urlWithParams = $"{linkSearch}?{queryString}";

            Logger.Log($"Build xong query cho hóa đơn {request.InvoiceNo} - {urlWithParams}");
            Logger.Log($"Bắt đầu Gọi API lên thuế Kiểm tra hóa đơn {request.InvoiceNo}");
            HttpResponseMessage response = await _client.GetAsync(urlWithParams);
            response.EnsureSuccessStatusCode();
            string stringContent = await response.Content.ReadAsStringAsync();
            Logger.Log($"Gọi API lên thuế Kiểm tra hóa đơn {request.InvoiceNo} hoàn tất");
            return JsonConvert.DeserializeObject<HoaDon>(stringContent);
        }
        public async Task<List<HoaDon>> GetInvoicesStatusAsync(List<TaxaxionRequest> requests, ProgressBarManager progressBarManager)
        {
            List<HoaDon> invoices = new List<HoaDon>();
            int total = requests.Count;
            int completed = 0;
            HoaDon invoice = null;
            try
            {
                // Use SemaphoreSlim to limit the number of concurrent tasks to 2
                var semaphore = new SemaphoreSlim(2);

                var tasks = requests.Select(async request =>
                {
                    await semaphore.WaitAsync(); // Wait until a slot is available (max 2 concurrent tasks)

                    try
                    {
                        Logger.Log($"Bắt đầu lấy captcha HDDT: {request.InvoiceNo}");
                        CaptchaResponse captcha = await GetCaptchaSvgFromWebsiteHDDT();
                        Logger.Log($"Lấy captcha từ HDDT thành công: {request.InvoiceNo}");

                        request.CaptchaKey = captcha.Key;
                        request.CaptchaText = captcha.Content;

                        invoice = await GetInvoiceNoAuthorizationAsync(request);

                    }
                    catch (Exception ex)
                    {
                        Logger.Log($"Lỗi {ex.Message}");
                    }
                    finally
                    {
                        lock (invoices) // Synchronize access to the list
                        {
                            if (invoice != null)
                            {
                                invoices.Add(invoice);
                            }
                            completed++;
                            progressBarManager.UpdateProgress(completed, total);
                        }
                        Logger.Log($"-----Hoàn thành {request.InvoiceNo} - {completed}");
                        semaphore.Release(); // Release the slot after processing
                    }
                }).ToArray();

                // Wait for all worker tasks to complete
                await Task.WhenAll(tasks);
            }
            catch (Exception ex)
            {
                Logger.Log($"Lỗi {ex.Message}");
            }

            return invoices;
        }
        public List<InvoiceBotResponse> MappingTaxaxionToInbot(List<HoaDon> invoices, RequestSyncTaxAuthority syncRequest)
        {
            try
            {
                var invoiceBotMapping = new List<InvoiceBotResponse>();

                invoices.ForEach(data =>
                {
                    //Map thông tin cơ bản của hóa đơn
                    var result = new InvoiceBotResponse()
                    {
                        //Thông tin người bán
                        SellerTaxCode = data.MSTNBan,
                        SellerName = data.TenNBan,
                        SellerAddress = data.DChiNBan,
                        SellerPhoneNumber = data.SDTNBan,
                        BuyerTaxCode = data.MSTNMua,
                        BuyerName = data.TenNNT,
                        BuyerAddress = data.DChiNMua,
                        BuyerPhoneNumber = data.SDTNMua,
                        TemplateNo = data.KyHieuMSoHDon == null ? "" : ((int)data.KyHieuMSoHDon).ToString(),
                        Series = data.KyHieuHDon,
                        InvoiceNo = data.SoHDon,
                        InvoiceDate = data.ThoiDiemLap.Value.AddHours(7),
                        PaymentMethod = data.TenHThucTT,
                        CcyCode = data.DonViTienTe,
                        ExchangeRate = Convert.ToDecimal(data.TyGia),
                        TotalAmountWithoutVat = data.TTienChuaThue == null ? 0 : (decimal)data.TTienChuaThue,
                        TotalVATAmount = data.TTienThue == null ? 0 : (decimal)data.TTienThue,
                        TotalDiscountAmount = data.TienCKhauTMai == null ? 0 : (decimal)data.TienCKhauTMai,
                        TotalAmount = data.TTienTToan == null ? 0 : (decimal)data.TTienTToan,
                        //Cập nhật lại tiền thanh toán để mang đi kiểm tra tồn tại trên TCT
                        InfoND123 = MappingND123InfoTaxaxionToInbot(data),
                        Items = MappingItemTaxaxionToInbot(data.ChiTiets),
                        MCCQT = data.MaHDonDTu,
                        StatusInvoice = data.TThai,
                        ProcessingStatus = data.TThaiXuLy,
                        CreatedDate = data.NgayTao.Value,
                        ModifiedDate = data.NgayCapNhat.Value,
                        IsRegisterCash = data.KyHieuHDon.IndexOf("M") == 3,
                        SubscriberId = syncRequest.SubscriberId,
                        OrgId = syncRequest.OrganizationId
                    };
                    result.InvoiceId = BuildInvoiceId(result.InvoiceNo, result.TemplateNo, result.Series, result.InvoiceDate, result.SellerTaxCode);
                    result.TransId = result.InvoiceId;

                    //Cập nhật thuế suất
                    if (result.InfoND123.ListVat.Count > 1) { result.VatRate = null; }
                    else if (result.InfoND123.ListVat.Count == 1) { result.VatRate = result.InfoND123.ListVat[0].VatRate; }

                    //Cập nhật thông tin CommonOthers
                    result.CommonOthers = MappingOtherInfors(data.TTinKhac);
                    invoiceBotMapping.Add(result);
                });

                return invoiceBotMapping;
            }
            catch (Exception ex)
            {
                Logger.Log(ex.Message);
            }
            return null;
        }
        private List<InvoiceItemInfo> MappingItemTaxaxionToInbot(List<HangHoaDVu> details)
        {
            var result = new List<InvoiceItemInfo>();
            try
            {
                if (details == null || details.Count() == 0) return null;
                int line = 1;
                foreach (var item in details)
                {
                    if (item == null) continue;
                    var newItem = new InvoiceItemInfo()
                    {
                        LineNumber = line,
                        Kind = item.TChat == null ? -1 : item.TChat.Value,
                        ItemCode = item.MaHHoaDVu,
                        ItemName = item.TenHHoaDVu,
                        UnitName = item.DViTinh,
                        Quantity = (decimal?)item.SoLuong,
                        UnitPrice = (decimal?)item.DGia,
                        AmountWithoutVat = (item.ThTien == null ? 0 : (decimal)item.ThTien) + (item.TienCKhau == null ? 0 : (decimal)item.TienCKhau),
                        VatRate = (decimal?)(item.TSuat != null ? item.TSuat * 100 : item.TSuat),
                        VatAmount = item.TThue == null ? 0 : (decimal)item.TThue,
                        DiscountRate = (decimal?)item.TyLeCKhau,
                        DiscountAmount = item.TienCKhau == null ? 0 : (decimal)item.TienCKhau,
                        Amount = item.ThTienSThue == null ? 0 : (decimal)item.ThTienSThue,
                        IsPromotion = item.TChat != null && (int)item.TChat == 2
                    };

                    //Nếu có tiền hàng và thuế suất, tính lại tiền thanh toán
                    if (newItem.AmountWithoutVat != 0 && newItem.VatAmount == 0 && newItem.VatRate > 0)
                    {
                        newItem.VatAmount = (newItem.AmountWithoutVat - newItem.DiscountAmount) * newItem.VatRate.Value / 100;
                    }
                    line++;
                    result.Add(newItem);
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex.Message);
            }
            return result;
        }
        private ND123Info MappingND123InfoTaxaxionToInbot(HoaDon data)
        {
            var result = new ND123Info();
            try
            {
                //Map danh sách tổng hợp thuế
                var vatInfos = new List<VatInfo>();
                if (data != null && data.TongHopThueSuat != null && data.TongHopThueSuat.Count > 0)
                {
                    foreach (var item in data.TongHopThueSuat)
                    {
                        if (item == null) continue;
                        var isVatOtherTemp = false;
                        var newVat = new VatInfo
                        {
                            VatRate = GetVatRate(item.TSuat, isVatOther => isVatOtherTemp = isVatOther),
                            TotalVATAmount = item.TThue == null ? 0 : (decimal)item.TThue,
                            TotalAmountWithoutVat = item.ThTien == null ? 0 : (decimal)item.ThTien,
                            IsVatOther = isVatOtherTemp
                        };
                        vatInfos.Add(newVat);
                    }
                }

                //Map thông tin hóa đơn liên quan
                var invConnect = new InvConnect()
                {
                    TemplateNo = data.KyHieuMSoHDonGoc,
                    Series = data.KyHieuHDonGoc,
                    InvoiceNo = data.SoHDonGoc,
                    InvoiceDate = data.NgayHDonGoc,
                    Noted = data.GhiChuHDonGoc
                };
                result.Nature = data.TChat;
                result.Status = data.TThai;
                result.ProcessingStatus = data.TThaiXuLy;
                result.ListVat = vatInfos;
                result.InvConnect = invConnect;
                //Thông tin vận chuyển
                result.ShipperName = data.NgVanChuyen;
                result.VehicleType = data.PhuongTienVanChuyen;
                result.ExportPerson = data.TenNgXuatHang;
                result.NumberAgreement = data.HopDongVanChuyen;
                result.InternalTransferOrder = data.LenhDieuDong;
                //Hợp đồng kinh tế số
                result.CommandOrder = data.HopDongKinhTe;
                result.CommandOrderDate = data.NgayHopDongKinhTe;
                //Thông tin người mua
                result.BuyerFullName = data.TenNMua;
                result.BankAccountBuyer = data.SoTKNMua;
                result.BankNameBuyer = data.NganHangNMua;
                //Thông tin người bán
                result.BankAccountSeller = data.SoTKNBan;
                result.BankNameSeller = data.NganHangNBan;
                //Thông tin bảng kê
                result.DeclarationNumber = data.SoBangKe;
                result.DeclarationDate = data.NgayBangKe;
                result.ReasonRejects = data.PdNdungs;

                //Map thêm danh sách các thông tin liên quan
                if (data.HoaDonLienQuans != null && data.HoaDonLienQuans.Count > 0)
                {
                    result.InvConnects = data.HoaDonLienQuans.Select(x => new InvConnect
                    {
                        TemplateNo = x.KyHieuMSoHDon != null ? ((int)x.KyHieuMSoHDon).ToString() : "",
                        Series = x.KyHieuHDon,
                        InvoiceNo = x.SoHDon,
                        Noted = x.GhiChuHDonGoc
                    }).ToList();
                }

                //Map thêm danh sách hóa đơn thông báo sai sót
                if (data.HoaDonThongBaoSaiSots != null && data.HoaDonThongBaoSaiSots.Count > 0)
                {
                    result.InvNotificationWrongs = data.HoaDonThongBaoSaiSots.Select(x => new InvConnect
                    {
                        TemplateNo = x.KyHieuMSoHDonGoc,
                        Series = x.KyHieuHDonGoc,
                        InvoiceNo = x.SoHDonGoc,
                        Noted = x.GhiChuHDonGoc,
                        ChangeDate = x.Ngay,
                        Type = x.Loai,
                        NotifyName = x.Ten,
                        NotifyNature = x.TinhChatTB.Value,
                        Reason = x.LyDo,
                        StatusInvoice = x.TThai,
                        StatusInvoiceProcess = x.TThaiXuLy
                    }).OrderBy(x => x.ChangeDate).ToList();
                }
            }
            catch (Exception)
            {

            }
            return result;
        }
        private List<OtherInfo> MappingOtherInfors(List<TTinKhac> tTinKhacs)
        {
            try
            {
                if (tTinKhacs == null && tTinKhacs.Count <= 0) return null;
                var otherInfos = new List<OtherInfo>();
                foreach (var tTinKhac in tTinKhacs)
                {
                    if (tTinKhac == null) continue;
                    var otherInfo = new OtherInfo { TTruong = tTinKhac.TenTruong, KDLieu = tTinKhac.KieuDLieu, DLieu = tTinKhac.DLieu };
                    otherInfos.Add(otherInfo);
                }
                return otherInfos;
            }
            catch (Exception)
            {

            }
            return null;
        }
        private decimal? GetVatRate(string stringVatRate, Action<bool> vatOtherCallback)
        {
            try
            {
                //Kiểm tra có phải thuế khác không
                vatOtherCallback?.Invoke(!string.IsNullOrEmpty(stringVatRate) && stringVatRate.Contains("khac"));
                if (string.IsNullOrEmpty(stringVatRate)) { return null; }
                if (stringVatRate.Equals("xxx"))
                {
                    return -4;
                }
                else if (stringVatRate.Equals("khac"))
                {
                    return -3;
                }
                else if (stringVatRate.Equals("kkknt"))
                {
                    return -2;
                }
                else if (stringVatRate.Equals("kct"))
                {
                    return -1;
                }
                else if (stringVatRate.Equals("0"))
                {
                    return 0;
                }
                else
                {
                    stringVatRate = stringVatRate.Replace("khac", "").Replace(":", "").Replace("%", "");
                    var check = decimal.TryParse(stringVatRate, out decimal vatRate);
                    if (check)
                    {
                        if (vatRate > 100)
                        {
                            var vatRateNew = Convert.ToDecimal(stringVatRate);
                            if (vatRateNew < 100) return vatRateNew;
                        }
                        else
                        {
                            return vatRate;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex.Message);
            }
            return null;
        }

        public async Task<HoaDon> GetInvoiceDetail(Dictionary<string, string> queryParams, string linkGetDetail)
        {
            ResetHeaderContent();
            try
            {
                Logger.Log("GetInvoiceDetail_Start post no XML");
                // build param
                string queryString = string.Join("&", queryParams.Select(kvp => $"{kvp.Key}={kvp.Value}"));
                string urlWithParams = linkGetDetail + "?" + queryString;
                var response = await CommonPattern.RetryOperationAsync(async () =>
                {
                    var httpResponse = await _client.GetAsync(urlWithParams);
                    if (!httpResponse.IsSuccessStatusCode)
                    {
                        throw new Exception($"HTTP request failed with status code {httpResponse.StatusCode}");
                    }
                    return httpResponse;
                });
                response.EnsureSuccessStatusCode();
                string stringContent = response.Content.ReadAsStringAsync().Result;
                Logger.Log("GetInvoiceDetail_Start post no XML Success");
                return JsonConvert.DeserializeObject<HoaDon>(stringContent);
            }
            catch (Exception ex)
            {
                Logger.Log(ex.Message);
                throw ex;
            }
        }

        /// <summary>
        /// Lấy hóa đơn theo khoảng thời gian
        /// </summary>
        /// <param name="baseUrl"></param>
        /// <param name="request"></param>
        /// <param name="currentDate"></param>
        /// <param name="endDate"></param>
        /// <returns></returns>
        private async Task<List<InvoicesResponse>> FetchInvoiceListAsync(string baseUrl, InvoiceInfoRequest request, DateTime currentDate, DateTime endDate)
        {
            ResetHeaderContent();

            // Tạo biểu thức tìm kiếm cho ngày hiện tại
            var textSearch = $"tdlap=ge={currentDate:dd/MM/yyyyT00:00:00};tdlap=le={endDate:dd/MM/yyyyT23:59:59}";


            // Trạng thái hóa đơn
            if (request.InvoiceStatus != 0)
            {
                textSearch += $";tthai=={request.InvoiceStatus}";
            }

            // Kết quả kiểm tra hóa đơn
            if (request.InvoiceCheckResult != -1)
            {
                // nếu khác tất cả
                textSearch += $";ttxly=={request.InvoiceCheckResult}";
            }
            else
            {
                // Nếu khác tất cả và là hóa đơn mua vào thì lấy cả 3 trạng thái
                // 5: "Đã cấp mã hóa đơn",
                // 6: "Tổng cục thuế đã nhận không mã",
                // 8: "Tổng cục thuế đã nhận hóa đơn có mã khởi tạo từ máy tính tiền"
                if (request.EinvoiceTab == 1)
                {
                    textSearch += $";ttxly=in=(5,6,8)";
                }
            }

            if (request.IsPaymentOrder && (baseUrl == DownloadEinvoiceSetting.LinkSoldInvoices || baseUrl == DownloadEinvoiceSetting.LinkPurchaseInvoices))
            {
                textSearch += $";unhiem==1";
            }

            var queryParams = new Dictionary<string, string>
            {
                { "sort", "tdlap:desc,khmshdon:asc,shdon:desc" },
                { "size", $"{request.Take}" },
                { "search", textSearch }
            };

            // Build parameter
            string urlWithParams = BuildQueryParam(baseUrl, queryParams);

            // check xem có trang tiếp theo hay không
            bool hasNextState = true;
            // state của kết quả trả về hiện tại là param của trang sau
            string currentState = null;
            // danh sách hóa đơn của tháng đó 
            List<InvoicesResponse> invoicesInRange = new List<InvoicesResponse>();

            while (hasNextState)
            {
                // build lại param lấy trang tiếp theo dựa trên state
                if (!string.IsNullOrEmpty(currentState))
                {
                    queryParams["state"] = currentState;
                    urlWithParams = BuildQueryParam(baseUrl, queryParams);
                }

                try
                {
                    var stringContent = "";
                    var response = await CommonPattern.RetryOperationAsync(async () =>
                    {
                        var httpResponse = await _client.GetAsync(urlWithParams);
                        stringContent = await httpResponse.Content.ReadAsStringAsync();
                        if (!httpResponse.IsSuccessStatusCode)
                        {
                            throw new Exception($"FetchInvoiceListAsync failed with status code {httpResponse.StatusCode} - Message: {stringContent} - {JsonConvert.SerializeObject(queryParams)}");
                        }
                        httpResponse.EnsureSuccessStatusCode();
                        return httpResponse;
                    });

                    var invoiceResponse = JsonConvert.DeserializeObject<InvoicesResponse>(stringContent);
                    currentState = invoiceResponse.State;

                    // Thêm hóa đơn vào danh sách
                    invoicesInRange.Add(invoiceResponse);
                    hasNextState = !string.IsNullOrEmpty(currentState) && request.Take != 1;
                    Logger.Log($"Get invoices params: {urlWithParams} success");
                }
                catch (Exception ex)
                {
                    hasNextState = false;
                    Logger.Log(ex.Message);
                    throw ex;
                }
            }

            return invoicesInRange;
        }

        private SemaphoreSlim semaphore = new SemaphoreSlim(15);

        /// <summary>
        /// Tải XML
        /// </summary>
        /// <param name="requests"></param>
        /// <param name="progressBarManager"></param>
        /// <returns></returns>
        public async Task<List<string>> DownloadXmlAsync(List<HoaDon> invoices, RequestSyncTaxAuthority syncRequest)
        {
            // tạo folder lưu file xml, pdf phục vụ sau này
            string folderPath = $"{Application.StartupPath}/HDDT/{_username}/Invoices";
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", syncRequest.Authorization);
            httpClient.DefaultRequestHeaders.Add("X-Misa-Context", syncRequest.XMISAContext);

            // số lượng tải thành công
            int totalSuccess = 0;
            // số lượng tải thất bại
            int totalError = 0;

            // Create a list to store the tasks for downloading XML files
            var downloadTasks = new List<string>();

            for (int i = 0; i < invoices.Count; i++)
            {
                try
                {
                    //await semaphore.WaitAsync(); // Chờ cho đến khi có một vị trí trống để chạy một Task
                    var invoice = invoices[i];

                    var queryParams = new Dictionary<string, string> {
                            { "nbmst", invoice.MSTNBan }, // MSTNBan
                            { "khhdon", invoice.KyHieuHDon }, // Series
                            { "shdon", invoice.SoHDon }, // InvoiceNo
                            { "khmshdon", invoice.KyHieuMSoHDon.ToString() } // TemplateNo
                        };

                    // check xem nếu trong ký hiệu hóa đơn ký tự thứ 3 là M thì thay đổi link tải
                    var linkExportXml = invoice.KyHieuHDon.IndexOf("M") == 3 ? DownloadEinvoiceSetting.LinkExportXmlFromCashRegister : DownloadEinvoiceSetting.LinkExportXml;

                    // build query param
                    string urlWithParams = BuildQueryParam(linkExportXml, queryParams);

                    // tải xml, nếu sau 10 lần tải thất bại sẽ trả về exception do lỗi
                    // trả về task là string
                    var response = await CommonPattern.RetryOperationAsync(async () =>
                    {
                        Logger.Log($"DownloadXmlAsync_{urlWithParams}");
                        // reset lại header mới cho mỗi lần tải
                        ResetHeaderContent();
                        // thời gian tải xml timeout tối đa là 2s
                        _client.Timeout = TimeSpan.FromSeconds(2);
                        var httpResponse = await _client.GetAsync(urlWithParams);

                        if (!httpResponse.IsSuccessStatusCode)
                        {
                            string errorContent = await httpResponse.Content.ReadAsStringAsync();
                            if (!string.IsNullOrEmpty(errorContent))
                            {
                                var errorMessage = JsonConvert.DeserializeObject<BaseTaxaxionResponse>(errorContent);
                                if (!string.IsNullOrEmpty(errorMessage.Message))
                                {
                                    //if (errorMessage.Message.ToLower().Contains("không tồn tại hồ sơ gốc của hóa đơn"))
                                    //{
                                    //    Logger.Log($"XML Does not exists {JsonConvert.SerializeObject(queryParams)}");
                                    //}
                                    //else
                                    //{

                                    //}
                                    totalError++;
                                    // break retry luôn nếu không tìm thấy thông tin hóa đơn
                                    throw new DownloadXMLException($"DownloadXmlAsync_Response from HDDT : {errorMessage.Message} - {JsonConvert.SerializeObject(queryParams)}");
                                }
                            }
                            else
                            {
                                throw new Exception($"DownloadXmlAsync_Request failed with status code {httpResponse.StatusCode} - {JsonConvert.SerializeObject(queryParams)}");
                            }
                        }

                        httpResponse.EnsureSuccessStatusCode();
                        return httpResponse;
                    });

                    byte[] responseBodyBytes = await response.Content.ReadAsByteArrayAsync();
                    downloadTasks.Add(ExtractToXml(responseBodyBytes, invoice, folderPath, httpClient, syncRequest));
                    Logger.Log($"DownloadXmlAsync_{urlWithParams} - success");

                    totalSuccess++;
                    // tải 5 cái delay 0.5s
                    if ((i + 1) % 5 == 0)
                    {
                        await Task.Delay(500);
                    }
                }
                catch (Exception ex)
                {
                    totalError++;
                    Logger.Log($"DownloadXmlAsync_{ex.Message}");
                }
            }
            Logger.Log($"DownloadXmlAsync_Finish to download xml: {totalSuccess}/{invoices.Count()} - Failed: {totalError}");
            Logger.Log($"{JsonConvert.SerializeObject(downloadTasks.ToList())}");
            return downloadTasks.ToList();
        }

        /// <summary>
        /// Giải nén folder và tìm ra file xml để gửi sang kế toán
        /// </summary>
        /// <param name="zipData"></param>
        /// <param name="request"></param>
        /// <returns></returns>
        private string ExtractToXml(byte[] zipData, HoaDon request, string folderPath, HttpClient httpClient, RequestSyncTaxAuthority syncRequest)
        {
            try
            {
                // gửi formData
                // giải nén file
                using (var zipStream = new MemoryStream(zipData))
                using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Read))
                {
                    // Duyệt qua các file trong file zip để tìm html và xml
                    foreach (var entry in archive.Entries)
                    {
                        // lấy ra extention
                        var extension = Path.GetExtension(entry.Name);
                        // lấy ra tên file
                        var fileName = Path.GetFileName(entry.Name);
                        var tmpXmlFolder = $"{folderPath}/{request.MSTNBan}";
                        if (!Directory.Exists(tmpXmlFolder))
                        {
                            Directory.CreateDirectory(tmpXmlFolder);
                        }
                        string filePath = $"{tmpXmlFolder}/{request.KyHieuMSoHDon}_{request.KyHieuHDon}_{request.SoHDon}_{fileName}";
                        if (extension.ToLower() == ".xml")
                        {
                            using (var fileStream = entry.Open())
                            using (var reader = new StreamReader(fileStream, Encoding.UTF8)) // Use UTF-8 encoding for XML
                            {
                                string xmlContent = reader.ReadToEnd();
                                return xmlContent;
                            }
                            //using (var fileStream = entry.Open())
                            //using (var memoryStream = new MemoryStream())
                            //{
                            //    fileStream.CopyTo(memoryStream);
                            //    byte[] fileBytes = memoryStream.ToArray();
                            //    File.WriteAllBytes(filePath, fileBytes);
                            //    semaphore.Release();
                            //    return fileBytes;
                            //}
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex.Message);
                throw ex;
            }
            return null;
        }

        /// <summary>
        /// Đồng bộ danh sách hóa đơn từ CQT
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        public async Task<List<HoaDon>> SyncInvoiceInRange(RequestSyncTaxAuthority syncRequest)
        {
            try
            {
                ResetHeaderContent();
                // tham số để lấy hóa đơn
                InvoiceInfoRequest request = new InvoiceInfoRequest()
                {
                    StartDate = syncRequest.StartDate,
                    EndDate = syncRequest.EndDate,
                    EinvoiceTab = syncRequest.IsSyncFromInputInvoice ? 1 : 0,
                    IsPaymentOrder = syncRequest.IsPaymentOrder,
                };

                var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", syncRequest.Authorization);
                httpClient.DefaultRequestHeaders.Add("X-Misa-Context", syncRequest.XMISAContext);

                //Logger.Log($"SyncInvoiceInRange_Request Client {JsonConvert.SerializeObject(_client)}");
                //Logger.Log($"SyncInvoiceInRange_Request Client to ASP {JsonConvert.SerializeObject(httpClient)}");
                //Logger.Log($"SyncInvoiceInRange_Request param {JsonConvert.SerializeObject(request)}");


                string linkInvoice = $"{DownloadEinvoiceSetting.LinkPurchaseInvoices}";
                string linkInvoiceFromCashRegister = $"{DownloadEinvoiceSetting.LinkPurchaseInvoicesFromCashRegister}";

                // nếu là hóa đơn bán ra thì sửa lại link lấy trên danh sách
                if (request.EinvoiceTab == 0)
                {
                    linkInvoice = $"{DownloadEinvoiceSetting.LinkSoldInvoices}";
                    linkInvoiceFromCashRegister = $"{DownloadEinvoiceSetting.LinkSoldInvoicesFromCashRegister}";
                }

                // hóa đơn thường
                List<InvoicesResponse> invoices = new List<InvoicesResponse>();
                // hóa đơn khởi tạo từ máy tính tiền
                List<InvoicesResponse> invoicesFromCashRegister = new List<InvoicesResponse>();

                DateTime currentDate = request.StartDate.Date;
                DateTime endDate = request.EndDate.Date;

                var lstXMLDownload = new List<HoaDon>();

                // Lấy Hóa đơn điện tử
                invoices = await FetchInvoiceListAsync(linkInvoice, request, currentDate, endDate);

                if (invoices != null && invoices.Count > 0)
                {
                    invoices.ForEach(x =>
                    {
                        lstXMLDownload.AddRange(x.Datas);
                    });
                }

                // Nếu không phải hóa đơn ủy nhiệm và có hóa đơn khởi tạo từ máy tính tiền
                if (!request.IsPaymentOrder)
                {
                    invoicesFromCashRegister = await FetchInvoiceListAsync(linkInvoiceFromCashRegister, request, currentDate, endDate);

                    if (invoicesFromCashRegister != null && invoicesFromCashRegister.Count > 0)
                    {
                        invoicesFromCashRegister.ForEach(x =>
                        {
                            lstXMLDownload.AddRange(x.Datas);
                        });
                    }
                }

                // mapping list inv crwal được sang bên inbot
                var invoiceBotMapping = MappingTaxaxionToInbot(lstXMLDownload, syncRequest);

                // DOWNLOAD XML 
                List<string> lstXmlString = null;
                if (lstXMLDownload.Count > 0)
                {
                    lstXmlString = await DownloadXmlAsync(lstXMLDownload, syncRequest);
                }

                Logger.Log($"{currentDate} - Total: {lstXMLDownload.Count}");

                return lstXMLDownload;

                //await CommonPattern.RetryOperationAsync(async () =>
                //{
                //    var taxAuthorityResponse = new TaxAuthoritySyncMaster
                //    {
                //        CreatedDate = currentDate,
                //        TaxCode = syncRequest.TaxCode,
                //        SubscriberId = syncRequest.SubscriberId,
                //        OrganizationId = syncRequest.OrganizationId,
                //        TotalCrawl = invoiceBotMapping.Count,
                //        TotalResponse = invoices.FirstOrDefault().Total.Value + invoicesFromCashRegister.FirstOrDefault().Total.Value,
                //        Invoices = invoiceBotMapping,
                //        Id = BuildSyncHistoryMasterId(syncRequest.SubscriberId, syncRequest.OrganizationId, currentDate)
                //    };

                //    var byteContent = new ByteArrayContent(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(taxAuthorityResponse)));
                //    byteContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                //    var response = await httpClient.PostAsync(syncRequest.APIUpdateHistoryMaster, byteContent);
                //    response.EnsureSuccessStatusCode();
                //    isPostSuccess = true;
                //    Logger.Log($"Finished to post Invoice {currentDate} - Total: {lstXMLDownload.Count} - Total In HDDT: {invoices.Sum(x => x.Total) + invoicesFromCashRegister.Sum(x => x.Total)}");
                //    return response;
                //});
            }
            catch (Exception ex)
            {
                Logger.Log(ex.Message);
                throw ex;
            }
        }
        private string BuildInvoiceId(string invoiceNo, string templateNo, string series, DateTime invoiceDate, string sellerTaxCode)
        {
            // Kết hợp thông tin vào một chuỗi
            string combinedInfo = $"{invoiceNo}-{templateNo}-{series}-{invoiceDate:yyyy-MM-dd}-{sellerTaxCode}";
            return BuildUniqueId(combinedInfo);
        }
        private string BuildSyncHistoryMasterId(string subscriberId, string organizationId, DateTime invoiceDate)
        {
            // Kết hợp thông tin vào một chuỗi
            string combinedInfo = $"{subscriberId}-{organizationId}-{invoiceDate:yyyy-MM-dd}";
            return BuildUniqueId(combinedInfo);
        }
        private string BuildUniqueId(string textBuild)
        {
            // Tạo một mã băm từ chuỗi kết hợp
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(textBuild));
                string uniqueId = BitConverter.ToString(hashBytes).Replace("-", "").ToLower().Substring(0, 24);
                return uniqueId;
            }
        }

    }
}