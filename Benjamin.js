/**
 * @description: Benjamin - A tool to download captcha images and solve them
 * you can use it to download captcha images and train your own captcha solver
 * @param: {any}
 */
class Benjamin {
    /**
     * The list of JS CDN scripts to load
     */
    jsCDN = [
        'https://cdnjs.cloudflare.com/ajax/libs/jszip/3.10.1/jszip.min.js',
        'https://cdn.jsdelivr.net/npm/sweetalert2@11',
    ]
    cssCDN = []
    defaultStyle = `
        .swal2-container {
            font-family: 'Roboto', sans-serif;
        }

        .benjamin-popup-content .swal2-input {
            text-align: center;
            margin: 10px;
            padding: 0;
            height: auto;
        }

        .benjamin-captcha-result {
            display: flex;
            justify-content: center;
            align-items: center;
        }

        .benjamin-captcha-result img {
            margin-right: 10px;
        }
    `
    captchaImageLink = ''
    numberOfImages = 10
    delayGetCaptchaImage = 0
    delaySolveCaptcha = 0
    isShowPopup = false
    swal = null

    /**
     * Constructor
     * Load JS CDN scripts
     */
    constructor(
        captchaImageLink = '',
        numberOfImages = 10,
        delayGetCaptchaImage = 0,
        delaySolveCaptcha = 0
    ) {
        this.captchaImageLink = captchaImageLink
        this.numberOfImages = numberOfImages
        this.delayGetCaptchaImage = delayGetCaptchaImage
        this.delaySolveCaptcha = delaySolveCaptcha
        this.loadStyle()
        this.init()
    }

    /**
     * @description: Load style
     * @param: {any}
     * Author: AnhDV 30/01/2024
     */
    loadStyle = () => {
        const me = this
        const style = document.createElement('style')
        style.innerHTML = me.defaultStyle
        document.head.appendChild(style)
    }

    /**
     * @description: Init Benjamin
     * @param: {any}
     */
    init = async () => {
        const me = this
        await me.loadExternalExternalScritps('js', me.jsCDN)
        await me.loadExternalExternalScritps('css', me.cssCDN)
        me.showPopupDownload()
    }

    /**
     * @description: Show popup download captcha images
     * @param: {any}
     */
    showPopupDownload = () => {
        const me = this
        // get currnt location
        if (!me.captchaImageLink) {
            me.captchaImageLink = window.location.href
        }
        // confirm before download
        Swal.fire({
            icon: 'question',
            title: 'Download captcha images?',
            html: `<div class="benjamin-popup-content">
                <div class="benjamin-popup-text"> 
                    <label for="benjamin-number-of-images">Number of images</label>
                    <input class="swal2-input" type="number" value="${me.numberOfImages}" id="benjamin-number-of-images"> 
                    <label for="benjamin-captcha-image-link">Captcha image link</label>
                    <input class="swal2-input" type="text" value="${me.captchaImageLink}" id="benjamin-captcha-image-link" >
                    <label for="benjamin-delay-get-captcha-image">Delay fetch captcha </label>
                    <input class="swal2-input" type="number" value="${me.delayGetCaptchaImage}" id="benjamin-delay-get-captcha-image" >
                    <label for="benjamin-delay-solve-captcha">Delay solve captcha</label>
                    <input class="swal2-input" type="number" value="${me.delaySolveCaptcha}" id="benjamin-delay-solve-captcha" >
                </div>
            </div>`,
            showConfirmButton: true,
            confirmButtonText: 'Download',
            showCancelButton: true,
            showCloseButton: false,
            allowOutsideClick: false,
        }).then((result) => {
            if (result.isConfirmed) {
                me.numberOfImages = document.querySelector(
                    '#benjamin-number-of-images'
                ).value
                me.captchaImageLink = document.querySelector(
                    '#benjamin-captcha-image-link'
                ).value
                me.delayGetCaptchaImage = document.querySelector(
                    '#benjamin-delay-get-captcha-image'
                ).value
                me.delaySolveCaptcha = document.querySelector(
                    '#benjamin-delay-solve-captcha'
                ).value
                me.downloadCaptchaImages()
            }
        })
    }

    /**
     * Generate uuidv4
     *
     * @returns {string} - The generated uuidv4 string
     */
    uuidv4 = () => {
        return '10000000-1000-4000-8000-100000000000'.replace(/[018]/g, (c) =>
            (
                c ^
                (crypto.getRandomValues(new Uint8Array(1))[0] & (15 >> (c / 4)))
            ).toString(16)
        )
    }

    /**
     * Load JS CDN scripts
     *
     * @returns {Promise<void>} - A Promise that resolves when all scripts are loaded
     */
    loadExternalExternalScritps = async (type = 'js', cdns = this.jsCDN) => {
        const typeText = type === 'js' ? 'scripts' : 'stylesheets'
        if (!cdns || cdns.length === 0) {
            return Promise.resolve()
        }
        const loadScript = (url) => {
            return new Promise((resolve, reject) => {
                let script = document.createElement('script')
                script.type = 'text/javascript'

                if (type === 'css') {
                    script = document.createElement('link')
                    script.type = 'text/css'
                }

                if (script.readyState) {
                    // IE
                    script.onreadystatechange = function () {
                        if (
                            script.readyState === 'loaded' ||
                            script.readyState === 'complete'
                        ) {
                            script.onreadystatechange = null
                            resolve()
                        }
                    }
                } else {
                    // Others
                    script.onload = function () {
                        resolve()
                    }
                }

                script.onerror = function () {
                    reject(new Error(`Failed to load ${typeText}: ${url}`))
                }

                if (type === 'css') {
                    script.href = url
                } else {
                    script.src = url
                }
                document.getElementsByTagName('head')[0].appendChild(script)
            })
        }

        const loadPromises = cdns.map(loadScript)

        // Wait for all scripts to be loaded
        await Promise.all(loadPromises)
        console.log(`All ${typeText} loaded successfully`)
    }

    /**
     * @description: Show popup
     * @param: {any}
     */
    showPopup = async (text, customHtml = '') => {
        const me = this
        if (me.isShowPopup) {
            document.querySelector('.benjamin-popup-text').innerHTML = text
            document.querySelector('.benjamin-popup-extra').innerHTML =
                customHtml
            return
        }
        me.isShowPopup = true
        me.swal = Swal.fire({
            icon: 'info',
            title: 'Downloading images and solving captchas...',
            html: `<div class="benjamin-popup-content">
                <div class="benjamin-popup-extra"></div>
                <div class="benjamin-popup-text">${text}</div>
            </div>`,
            allowOutsideClick: false,
            showConfirmButton: false,
            showCancelButton: false,
            showCloseButton: false,
        })
    }

    /**
     * @description: Close popup
     * @param: {any}
     */
    closePopup = () => {
        const me = this
        if (me.swal) {
            me.swal.close()
            me.isShowPopup = false
            me.swal = null
        }
    }

    /**
     * Download captcha image
     *
     * @param {string} imageLink - The URL of the image to download
     * @returns {Promise<void>} - A Promise that resolves when the download is complete
     */
    downloadCaptchaImages = async (
        imageLink = this.captchaImageLink,
        numberOfImages = this.numberOfImages
    ) => {
        let me = this
        try {
            const zip = new JSZip()
            var files = [],
                downloadImageSuccess = 0,
                downloadImageFailed = 0
            me.showPopup(
                `Downloading ${numberOfImages} images - 0 success, 0 failed`
            )
            for (let i = 0; i < numberOfImages; i++) {
                try {
                    const response = await fetch(imageLink)
                    if (!response.ok) {
                        throw new Error('Failed to fetch image')
                    }
                    const blob = await response.blob()
                    files.push({
                        name: `${this.uuidv4()}_${i + 1}.png`,
                        blob,
                    })
                    downloadImageSuccess++
                    if (me.delayGetCaptchaImage) {
                        await new Promise((resolve) =>
                            setTimeout(resolve, me.delayGetCaptchaImage)
                        )
                    }
                } catch (error) {
                    console.error('Error downloading image:', error)
                    downloadImageFailed++
                } finally {
                    me.showPopup(
                        `Downloading ${numberOfImages} images - ${downloadImageSuccess} success, ${downloadImageFailed} failed`
                    )
                }
            }

            // Add all images to the zip file
            await me.processImagesWithGradio(files)
            files.forEach((file) => {
                zip.file(file.name, file.blob)
            })

            // Generate the zip file asynchronously
            zip.generateAsync({ type: 'blob' }).then((content) => {
                // Create a URL for the Blob
                const url = window.URL.createObjectURL(content)

                // Create an 'a' element to create a download link and trigger a click on it
                const link = document.createElement('a')
                link.href = url
                link.download = `${this.uuidv4()}_images.zip`
                document.body.appendChild(link)
                link.click()

                // Revoke the URL object and remove the 'a' element after the download
                window.URL.revokeObjectURL(url)
                document.body.removeChild(link)

                console.log('Images downloaded and zipped successfully')
            })
        } catch (error) {
            console.error('Error downloading images:', error)
            me.closePopup()
        }
    }

    /**
     * Process images with Gradio after obtaining captchas
     *
     * @param {FileList} images - The list of images to process
     * @returns {Promise<void>} - A Promise that resolves when the processing is complete
     * @throws {Error} - If there is an error during processing
     */
    processImagesWithGradio = async (images) => {
        let success = 0,
            failed = 0,
            me = this
        try {
            me.showPopup(`Processing solved ${images.length} captchas`)

            const gradioModule = await import(
                'https://cdn.jsdelivr.net/npm/@gradio/client@0.1.4/dist/index.min.js'
            )

            const app = await gradioModule.client(
                'https://docparser-text-captcha-breaker.hf.space/'
            )

            for (let i = 0; i < images.length; i++) {
                let image = images[i].blob,
                    newName = images[i].name,
                    extension = newName.split('.')[1]

                try {
                    const result = await app.predict('/predict', [image])
                    success++
                    newName = result.data[0]
                    if (me.delaySolveCaptcha) {
                        await new Promise((resolve) =>
                            setTimeout(resolve, me.delaySolveCaptcha)
                        )
                    }
                } catch (error) {
                    failed++
                } finally {
                    images[i].name = newName + '.' + extension
                    console.log(
                        `Decaptcha ${i + 1} of ${
                            images.length
                        } complete - ${success} success, ${failed} failed`
                    )
                    me.showPopup(
                        `Processing solved ${images.length} captchas - ${success} success, ${failed} failed`,
                        `<div class="benjamin-captcha-result">
                            <img src="${URL.createObjectURL(
                                image
                            )}" width="100" /> 
                            <span> Result: ${newName} </span>
                         </div>`
                    )
                }
            }
        } catch (error) {
            console.error('Error processing images with Gradio:', error.message)
            throw error
        } finally {
            me.closePopup()
            Swal.fire({
                icon: 'success',
                title: 'Captcha images downloaded and solved successfully',
                text: `Downloaded ${images.length} images - ${success} success, ${failed} failed`,
                showConfirmButton: true,
                confirmButtonText: 'Download again',
                showCancelButton: true,
                cancelButtonText: 'Close',
                showCloseButton: false,
                allowOutsideClick: false,
            })
                .then((result) => {
                    if (result.isConfirmed) {
                        me.showPopupDownload()
                    }
                })
                .catch((error) => {
                    console.error('Error showing success popup:', error)
                })
        }
    }
}

// Run
benjamin = new Benjamin()
