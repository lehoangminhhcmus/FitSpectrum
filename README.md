# PhổFit

Ứng dụng C# WinForms cho Windows có .NET Framework 4.x. Mở PhoFit-Colegram.exe để chạy.
Mã nguồn đi kèm. Trong thư mục đã giải nén, chạy `powershell -File .\build.ps1`.
Thêm `-Test` để tạo phổ mẫu và chạy kiểm tra tính toán.

## Nhập phổ

- Một cột số đếm: gán kênh từ 0.
- Hai cột X và số đếm: X là kênh hoặc năng lượng, tăng nghiêm ngặt. X có thể cách nhau không đều.
- Tách cột bằng dấu cách, tab, phẩy hoặc chấm phẩy. Dùng dấu chấm thập phân. Có thể dùng tiêu đề như `energy,counts`, dòng trống hoặc chú thích bắt đầu bằng #.
- Số đếm âm được chấp nhận cho bình phương tối thiểu và chi bình phương, còn Poisson cần số đếm không âm.

## Khớp hai đỉnh trên phổ mẫu

1. Mở demo-counts.txt. Đặt ROI từ 60 đến 145 rồi bấm **Áp dụng**, hoặc kéo trên cửa sổ Phổ toàn phần.
2. Thêm POLY để mô tả nền. Mặc định chỉ c0 được thả tự do.
3. Nhấp trong cửa sổ ROI gần kênh 95, chọn G và bấm **Thêm hàm**. Làm tương tự gần 104.
4. Bấm **LS**, **χ²** hoặc **Poisson** trên thanh công cụ. Tâm kỳ vọng là 95 và 104, diện tích gần 2506.6 và 1203.2 số đếm.
5. Xem số liệu trong bảng tham số. Đường xanh là tổng mô hình, đường chấm là từng thành phần. Cửa sổ Phần dư có đường đỏ tại mốc 0. Cuộn để thu phóng, nhấp phải để đặt lại.

Với demo-energy.csv, dùng ROI 50–92.5 và tâm khởi tạo 67.5 và 72.
Mẫu được sinh từ hàm, có số đếm phân số và không có nhiễu.

## Mô hình và tham số

- Đỉnh G, TG, DTG, ALPHA, ALPHA3, V, TV, DTV, L, GS, VS, AS, A3S, HYPER, HYPERV, LOWE, LOWEV.
- Nền POLY (bậc tối đa 5), EXPO, STEP, DSTEP. POLY và EXPO dùng trục chuẩn hóa u = (X − cận trái ROI)/(cận phải − cận trái).
- BETA1 đến BETA7 lần lượt là allowed, 1FNU, 1FU, 2FNU, 2FU, 3FNU, 3FU. X và endpoint tính bằng keV động năng. Đặt Z, số khối, điện tích −1 hoặc +1 và các hệ số hình dạng theo chuyển tiếp. Kiểm tra tham số beta trước khi dùng cho dữ liệu thực.
- Nhập Min/Max để giới hạn tham số, đánh dấu Cố định hoặc dùng cùng tên Nhóm để buộc các giá trị bằng nhau trong ROI. GS/VS hỗ trợ σ² = σ₀² + k·tâm. Mặc định k cố định ở 0. AS/A3S chia sẻ độ rộng trong từng nhóm.
- Chi phí: tổng bình phương phần dư, chi bình phương Neyman dùng max(1, số đếm) làm phương sai, hoặc Poisson deviance. Sai số 1σ là ước lượng cục bộ từ Jacobian và hiệp phương sai, cần xem cùng trạng thái hội tụ, đặc biệt khi tham số chạm biên. Diện tích là tổng số đếm dự đoán tại các mẫu trong ROI, kể cả với trục năng lượng không đều.
- Voigt dùng pseudo-Voigt. Các đuôi của biến thể Voigt dùng Gaussian chập hàm mũ. Công thức, tham số khởi tạo và kết quả số có thể khác Colegram.

## Tệp và xử lý

Lưu phổ và mô hình ROI vào .phofit. Sau khi mở dự án, khớp lại để tính phần dư và độ bất định. Xuất CSV tạo bảng tham số/diện tích và một tệp .curve.csv có phổ, tổng mô hình, phần dư và từng thành phần.
Khớp hàng loạt áp dụng cùng cấu hình ROI lên các tệp văn bản có cùng hệ tọa độ và xuất bảng CSV từng ROI.
Cộng phổ yêu cầu trục X giống nhau. Hiệu chuẩn tuyến tính hoặc bậc hai áp dụng cho phổ hiện tại và tạo ROI mới. Xuất trừ nền dùng thành phần nền đã khớp hoặc nền tích phân hai vòng theo phụ lục 3.

Phạm vi tương thích gồm các phổ văn bản một hoặc hai cột. Các định dạng máy đo chuyên dụng và .spm của Colegram cần được chuyển đổi trước. Bố cục nhiều cửa sổ dựa trên ảnh trong sổ tay Colegram và hướng dẫn khớp Gauss. Hiệu chuẩn hàng loạt chưa được tái tạo.

Tham chiếu mô hình: LNHB 2020/15, COLEGRAM User Manual, mục 3.2 và phụ lục 3.


## Bản 1.1: thư mục và thống kê

Xem `HUONG-DAN.txt` để cài đặt và dùng **Tệp > Khớp toàn bộ thư mục…**.
Tính năng này tạo duy nhất một CSV UTF-8 có BOM với một hàng cho mỗi thành phần
trong mỗi ROI/tệp, bao gồm diện tích, độ bất định, R², χ² Neyman, χ²/bậc tự do,
phương pháp và trạng thái/lỗi. Chỉ xét tệp ở cấp thư mục hiện tại.
R² dùng phần dư thô và không xác định khi phổ hằng. χ² dùng phương sai max(1,y)
cho mọi phương pháp. Các chỉ số cũng hiện ở báo cáo và CSV vùng hiện tại.

Build Windows: `powershell -ExecutionPolicy Bypass -File .\build.ps1 -Test`.
Để tạo bộ cài, chép exe mới vào `dist`, tạo thư mục `artifacts`, cài NSIS 3,
rồi chạy `makensis packaging\installer.nsi`. Bộ cài theo tài khoản hiện tại,
có shortcut và chức năng gỡ cài đặt, yêu cầu .NET Framework 4.5+.

## Bản 1.1.2: sửa lỗi mở ứng dụng trên Windows

Sửa ba lời gọi String.Split thành overload char[] có trong .NET Framework.
Bản phát hành 1.1.2 được biên dịch với `-nostdlib` và bộ reference assemblies
Microsoft.NETFramework.ReferenceAssemblies.net45 1.0.3, tránh vô tình sử dụng API
chỉ có trong thư viện runtime Mono. Chạy lại toàn bộ Smoke thành công.

## Bản 1.2.0: CSV đầy đủ cho các tệp được chọn

**Khớp hàng loạt…** chọn nhiều phổ rồi chọn một CSV chung. Dùng cùng bộ xuất
với **Khớp toàn bộ thư mục…**. `row_type` phân biệt `area`, `parameter`, `curve`,
`error`. Bao gồm đường dẫn nguồn, ROI, hàm, diện tích/sai số, giá trị tham số
ban đầu và sau fit, độ bất định, giới hạn, cố định/nhóm liên kết, R², χ²,
χ²/bậc tự do, chi phí, phương pháp, số vòng và trạng thái; đường khớp chứa
X/số đếm/tổng mô hình/phần dư/giá trị từng thành phần tại các mẫu trong ROI.
Từ chối đầu ra trùng bất kỳ tệp đầu vào nào trước khi ghi.

## Bản 1.2.1: độ rộng và hội tụ

Giới hạn thay đổi độ rộng tối đa 50% giá trị hiện tại trong một bước LM.
Từ chối khởi tạo độ rộng tự do dưới nửa bước đo; đánh dấu kết quả không hội tụ
khi độ rộng sau fit không đủ lấy mẫu hoặc Jacobian suy biến. Thêm nút
Ước lượng độ rộng cho các đỉnh G, ước lượng tâm/biên độ/FWHM theo dữ liệu
trong vùng giữa các tâm đỉnh, giữ giới hạn và tham số cố định.
Kiểm tra bổ sung phổ tổng hợp trên trục 2300–3200, biên độ hàng chục nghìn,
hai đỉnh sigma 60/75 với nền tuyến tính: cả ba phương pháp khôi phục đúng.

## Bản 1.2.2: CSV kết quả cuối gọn

Thay CSV chi tiết bằng một hàng cho mỗi tệp/ROI: tên tệp, chỉ số đánh giá cuối,
trạng thái và các cột diện tích/tham số cuối/độ bất định cho từng hàm.
Loại bỏ curve, giá trị ban đầu, cấu hình và thông tin vòng lặp khỏi CSV hàng loạt.
Giữ ghi lỗi và đánh dấu chưa hội tụ để phân biệt kết quả cần kiểm tra.

## Bản 1.2.3: Giới thiệu

Chọn **Trợ giúp → Giới thiệu…** để xem thông tin tác giả và mở liên kết email.

Lê Hoàng Minh<br>
Bộ môn Vật lý hạt nhân<br>
Khoa Vật lý-Vật lý Kỹ thuật<br>
Trường Đại học khoa học tự nhiên<br>
Đại học Quốc gia Thành phố Hồ Chí Minh<br>
email: [hoangminh@hcmus.edu.vn](mailto:hoangminh@hcmus.edu.vn)

Bộ cài hiện tại: [PhoFit-Setup-Windows-v1.2.3.exe](artifacts/PhoFit-Setup-Windows-v1.2.3.exe).

## Bản 1.2.4: tên hàm đầy đủ

Danh sách hàm và báo cáo/CSV hiển thị tên đầy đủ: Gaussian, Lorentzian,
Nền đa thức, Nền hàm mũ và tên mô tả đầy đủ cho các biến thể đuôi/độ rộng/phổ beta.
Mã mô hình nội bộ trong dự án được giữ nguyên để tương thích tệp cũ.
CSV hàng loạt vẫn chỉ xuất các giá trị cuối cho mỗi tệp/ROI.
