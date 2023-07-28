// _____________________________________________________________________________________________
// | Pattern            | Nhóm             | Mô tả và Vai trò của các thành phần                    |
// |-------------------------------------------------------------------------------------------     |
// | Composite          | Structural       | Component: Interface hoặc lớp trừu tượng đại diện      |
// | Pattern            |                  |           cho các thành phần (leaf) và đối tượng       |
// |                    |                  |           tổng hợp (composite).                        |
// |                    |                  | Leaf: Thành phần cuối cùng trong cây đối tượng,        |
// |                    |                  |       không có thành phần con.                         |
// |                    |                  | Composite: Đối tượng tổng hợp, chứa một tập hợp        |
// |                    |                  |           các thành phần con (leaf hoặc composite      |
// |                    |                  |           khác).                                       |
// |------------------------------------------------------------------------------------------------|
// | Singleton          | Creational       | Singleton Class: Lớp chứa một phương thức static       |
// | Pattern            |                  |                  trả về thể hiện duy nhất của lớp.     |
// |                    |                  | Lazy Initialization: Cơ chế để tạo thể hiện duy        |
// |                    |                  |                      nhất khi phương thức getInstance()|
// |                    |                  |                      được gọi lần đầu tiên.            |
// |                    |                  | Thread-Safety: Cơ chế để đảm bảo thread-safe khi       |
// |                    |                  |               tạo thể hiện.                            |
// |                    |                  | Eager Initialization: Khởi tạo thể hiện duy nhất       |
// |                    |                  |                      ngay khi lớp được tải vào bộ      |
// |                    |                  |                      nhớ.                              |
// |------------------------------------------------------------------------------------------------|
// | Prototype          | Creational       | Prototype Interface: Interface hoặc lớp trừu tượng     |
// | Pattern            |                  |                      đại diện cho các đối tượng có     |
// |                    |                  |                      thể sao chép.                     |
// |                    |                  | Concrete Prototypes: Các lớp triển khai của            |
// |                    |                  |                     Prototype Interface, sao chép      |
// |                    |                  |                     chính nó để tạo ra bản sao mới.    |
// |------------------------------------------------------------------------------------------------|
// | Observer           | Behavioral       | Subject: Đối tượng cần được theo dõi, duy trì danh     |
// | Pattern            |                  |           sách các Observer và cung cấp các            |
// |                    |                  |           phương thức để thêm, xóa và thông báo    	|
// |                    |                  |           cho các Observer khi có sự thay đổi.     	|
// |                    |                  | Concrete Subject: Lớp triển khai của Subject,      	|
// |                    |                  |                  chứa thông tin trạng thái và khi      |
// |                    |                  |                  trạng thái thay đổi, thông báo cho    |
// |                    |                  |                  các Observer đã đăng ký.           	|
// |                    |                  | Observer: Interface hoặc lớp trừu tượng đại diện   	|
// |                    |                  |           cho các đối tượng quan sát Subject,      	|
// |                    |                  |           chứa phương thức update() để nhận thông  	|
// |                    |                  |           báo từ Subject khi có sự thay đổi.      	|
// |                    |                  | Concrete Observer: Các lớp triển khai của Observer,	|
// |                    |                  |                    cập nhật trạng thái của mình khi	|
// |                    |                  |                    nhận thông báo từ Subject.       	|
// |------------------------------------------------------------------------------------------------|
// | Mediator           | Behavioral       | Mediator: Interface hoặc lớp trừu tượng chứa các  	    |
// | Pattern            |                  |            phương thức để xử lý giao tiếp giữa các	    |
// |                    |                  |            đối tượng.                            		|
// |                    |                  | Concrete Mediator: Lớp triển khai của Mediator,  		|
// |                    |                  |                   quản lý việc trao đổi thông tin 	    |
// |                    |                  |                   giữa các đối tượng.            		|
// |                    |                  | Colleague: Interface hoặc lớp trừu tượng đại diện 	    |
// |                    |                  |           cho các đối tượng cần giao tiếp với nhau	    |
// |                    |                  |           thông qua Mediator.                    		|
// |                    |                  | Concrete Colleagues: Các lớp triển khai của       	    |
// |                    |                  |                      Colleague, gửi thông điệp   		|
// |                    |                  |                      tới Mediator và nhận thông   	    |
// |                    |                  |                      điệp từ Mediator.           		|
// |------------------------------------------------------------------------------------------------|
// | State              | Behavioral       | State: Interface hoặc lớp trừu tượng đại diện cho  	|
// | Pattern            |                  |         các trạng thái của đối tượng, chứa các   	    |
// |                    |                  |         phương thức tương ứng với các hành vi          |
// |                    |                  |         cụ thể dựa trên trạng thái hiện tại của        |
// |                    |                  |         đối tượng.                                     |
// |                    |                  | Concrete States: Các lớp triển khai của State,         |
// |                    |                  |                đại diện cho các trạng thái cụ thể      |
// |                    |                  |                và triển khai các phương thức để        |
// |                    |                  |                thay đổi hành vi của đối tượng khi      |
// |                    |                  |                ở trong trạng thái đó.                  |
// |                    |                  | Context: Đối tượng cần thay đổi hành vi dựa trên       |
// |                    |                  |          trạng thái nội tại của nó, chứa một           |
// |                    |                  |          trạng thái hiện tại và triển khai các         |
// |                    |                  |          phương thức để thực hiện hành vi tương        |
// |                    |                  |          ứng dựa trên trạng thái đó.                   |
// |------------------------------------------------------------------------------------------------|
// | Template Method
