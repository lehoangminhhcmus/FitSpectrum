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

