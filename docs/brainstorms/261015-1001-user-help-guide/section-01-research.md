# Tham khảo: MISA, Fast, Bravo và công cụ làm video

> Thuộc brainstorm [Hướng dẫn sử dụng theo từng màn hình](SUMMARY.md) · Tra cứu ngày 2026-10-15. Kết quả lấy từ trang chính thức khi mở được, còn lại từ bài của bên thứ ba. Những chỗ chưa kiểm chứng có ghi rõ.

## Ba phần mềm kế toán Việt Nam

| Hãng | Trong phần mềm | Trang hướng dẫn | Video |
|---|---|---|---|
| **MISA SME** | F1 hoặc menu Trợ giúp → Nội dung trợ giúp | [helpsme.misa.vn/2026](https://helpsme.misa.vn/2026/). Chia theo chủ đề và nghiệp vụ (Bắt đầu sử dụng, Hướng dẫn nghiệp vụ, Câu hỏi thường gặp, Kiểm tra đối chiếu báo cáo). Có ô tìm kiếm. Bài có dạng `/2026/kb/<slug>/`, một số link vẫn trỏ về `/2023/kb/` | Kênh [@AMISKetoan](https://www.youtube.com/@AMISKetoan), có livestream. Trang hướng dẫn chỉ đặt link, không nhúng video |
| **MISA AMIS Kế toán** | Chat AMIS Support ở cạnh phải màn hình (tổng đài dừng từ 01/12/2025, theo [trang tổng đài MISA](https://sme.misa.vn/220867/tong-dai-misa/)), nút "Hỏi cộng đồng" | [help.amis.vn](https://help.amis.vn/ac/ke-toan), [helpact.misa.vn](https://helpact.misa.vn/). Chia theo nghiệp vụ, có mục "Hướng dẫn qua Youtube" | Như trên |
| **Fast Accounting Online** | Không thấy hướng dẫn theo ngữ cảnh | [help.faonline.vn](https://help.faonline.vn/). Chia theo phân hệ (Tiền, Bán hàng, Mua hàng, Tồn kho…) và theo loại (Bắt đầu sử dụng, Báo cáo, Câu hỏi thường gặp). [Trang hỗ trợ](https://faonline.vn/ho-tro/) có thêm slide đào tạo theo phân hệ, nhóm Facebook và email | Kênh [@FastAccountingOnline](https://www.youtube.com/@FastAccountingOnline), [playlist cơ bản](https://www.youtube.com/playlist?list=PL78WiNWMqserz-kLeOeRpseHCnrSR1lJR). Nhiều video từ 2022 |
| **Bravo 8** | F1 là phím trợ giúp | [bravo8help.bravo.com.vn/?CommandKey=MainWindow](https://bravo8help.bravo.com.vn/?CommandKey=MainWindow). Mỗi màn hình hoặc lệnh có một khóa `CommandKey`, nên F1 mở đúng trang của màn đó. Trang không mở được khi tra cứu nên chưa xem được nội dung bài | Có video và hỗ trợ qua đội triển khai, theo [bài đánh giá](https://blog.mbw.vn/review-chi-tiet-bravo-co-nen-dau-tu-su-dung-bravo-erp/) |

**Rút ra cho QLBanHang:**
- Đáng học từ Bravo: mỗi màn hình có một khóa, F1 mở đúng hướng dẫn của màn đó. QLBanHang dùng route làm khóa.
- Đáng học từ Fast: playlist chia theo phân hệ. QLBanHang chia playlist theo các nhóm của sidebar.
- Cần tránh: tài liệu và video nằm tách khỏi phần mềm nên dễ cũ (link về bài bản cũ, video giao diện cũ). QLBanHang để nội dung trong repo, có test và video làm bằng code.
- Chưa hãng nào có tour dẫn từng bước trong app, theo những gì tra được.

## Công cụ làm video

| Công cụ | Cách làm | Ghi chú |
|---|---|---|
| Playwright + giọng đọc AI + ffmpeg (tự dựng) | Kịch bản thao tác là nguồn duy nhất. Có giọng cho từng cảnh, ffmpeg ghép. Giao diện đổi thì chạy lại | [Ví dụ quay demo bằng Playwright](https://flaviocopes.com/record-demo-video-playwright/). Phải đặt kích thước video, nếu không Playwright tự thu về 800×800. File video chỉ hoàn tất khi đóng context. **`recordVideo` cố định 1 Mbit/s, VP8, không chỉnh chất lượng được** ([issue #31424](https://github.com/microsoft/playwright/issues/31424)), nên QLBanHang chụp khung hình qua CDP screencast. Các bộ công cụ có sẵn cùng ý tưởng: [demofly](https://cdn.jsdelivr.net/npm/demofly@0.2.11/README.md), [narrated-demo](https://smithery.ai/skills/roaming-panda-llc/narrated-demo) (có hàm trả về độ dài lời đọc để canh cảnh) |
| Guidde, Clueso, Trupeer (SaaS) | Quay một lần, AI viết lời, lồng giọng, phóng to, xuất tài liệu từng bước | Số ngôn ngữ do nhà cung cấp tự công bố ([so sánh Clueso và Trupeer](https://www.clueso.io/vs/trupeer)). **Chưa xác nhận có tiếng Việt.** Tính phí theo người dùng hằng tháng |
| Scribe | Thiên về tài liệu từng bước bằng ảnh chụp | Gói Pro Team 12 USD/người/tháng, tối thiểu 5 người, theo [bài đánh giá của Guidde](https://www.guidde.com/knowledge-hub/best-ai-tools-instructional-video-creation-2026-guide) |
| Synthesia, HeyGen | Người dẫn ảo đứng nói | Loại: đắt, không giúp hiểu thao tác hơn |

## Giọng đọc tiếng Việt

Ứng viên để nghe thử: Azure Speech (vi-VN HoaiMy, NamMinh), Google Cloud TTS (vi-VN), FPT.AI hoặc Vbee (nhà cung cấp trong nước), ElevenLabs (đa ngôn ngữ). [Fliki](https://fliki.ai/voices/vietnamese) có giọng tiếng Việt của Microsoft, Google, ElevenLabs, OpenAI, Amazon Polly. Dự án mã nguồn mở [VieNeu-TTS](https://github.com/pnnbao97/VieNeu-TTS) chạy được trên máy, nhưng đọc từ tiếng Anh kém hơn.

Không tìm được bảng so sánh độc lập nào (điểm MOS, giá) cho tiếng Việt. Vì vậy chọn giọng bằng cách cho nghe thử cùng một đoạn có từ khó: mã phiếu, chữ viết tắt, phím tắt, đơn vị đo.

## Thư viện tour (cho đợt 4)

| Thư viện | Giấy phép |
|---|---|
| driver.js | MIT, không phụ thuộc framework |
| react-joyride | MIT. Có bài cho rằng chạy lỗi với React 19; QLBanHang đang dùng React 18 |
| Intro.js | AGPL, hoặc mua giấy phép thương mại |
| Shepherd.js | Các nguồn mâu thuẫn (MIT hoặc AGPL kèm bản thương mại). Phải đọc file LICENSE của bản định cài |

Nguồn: [so sánh của Inline Manual](https://inlinemanual.com/blog/driverjs-vs-introjs-vs-shepherdjs-vs-reactour), [Chameleon](https://www.chameleon.io/blog/javascript-product-tours). Phần mềm mã đóng nên chọn driver.js hoặc react-joyride.

## YouTube Data API

- Video tải lên qua `videos.insert` từ một project chưa được kiểm duyệt (tạo sau 28/07/2020) bị khóa ở chế độ riêng tư, dù có đặt `privacyStatus: public`. Muốn gỡ phải qua kiểm duyệt tuân thủ của Google ([tóm tắt](https://www.upload-post.com/youtube-api/), [hướng dẫn tự động tải lên](https://postproxy.dev/blog/automate-youtube-uploads/)).
- Hạn mức cho `videos.insert` đang được các nguồn ghi khác nhau (1.600 đơn vị mỗi lần, hoặc 100 lần mỗi ngày theo cơ chế mới từ 06/2026). Kiểm tra lại trên Cloud Console nếu làm đợt 4 ([outlierkit](https://outlierkit.com/resources/youtube-api-quota/)).
- YouTube không cho thay file của một video đã đăng, nên quay lại thì phải đăng thành video mới.
- Chương trong mô tả chỉ hiện khi chương đầu ở 0:00, có ít nhất 3 chương và mỗi chương dài ít nhất 10 giây ([Storyblocks](https://www.storyblocks.com/resources/tutorials/how-to-add-chapters-to-youtube-video), [Dickinson Media Center](https://blogs.dickinson.edu/mediacenter/2026/01/12/youtube-adding-video-chapters/)).
