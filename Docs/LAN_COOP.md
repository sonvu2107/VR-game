# LAN co-op hai người

## Map theo level và giao thức hiện tại

Level 1 dùng Dungeon gốc; Host sinh Arcane từ level 2 trong cùng phiên, không đổi scene hay ngắt socket. World packet gửi thêm mesh nền/tường/props và trạng thái ẩn/hiện Tilemap một lần mỗi tầng. Snapshot chỉ gửi UV của bẫy chuyển động (bốn đỉnh), không gửi lại toàn bộ mesh map mỗi frame. Client dựng hình từ dữ liệu Host, không chạy PCG hay collider/AI của Arcane. Mesh cũ được dọn khi đổi tầng hoặc rời phiên.

Giao thức đã nâng lên phiên bản 2; hai máy phải dùng cùng bản build mới. Bản LAN cũ bị từ chối khi bắt tay để tránh Client chỉ thấy nền dungeon cũ. Round-trip JSON trong Editor không thay thế kiểm thử hai máy Wi-Fi thực tế.

## Cách chạy

Mở StartMenu, bấm Start và chọn Chơi một mình / Tạo phòng LAN / Tham gia phòng.
Host dùng cổng TCP 7777 mặc định; Client nhập IPv4 Host và cùng cổng. Hai máy cần cùng bản build và cùng LAN. IP được hiển thị trên thanh phiên chơi; có thể dùng `ipconfig` để xem IPv4 của adapter mạng đang sử dụng. Cho phép game qua Windows Firewall trên mạng Private khi được hỏi. Bản build không tự thay đổi Firewall.

Không cần package mạng hoặc dịch vụ cloud. `GameManager.Awake` tự gắn `LanSessionBridge` theo lựa chọn phiên chơi. Các scene và prefab hiện có được giữ nguyên.

## Quyền điều khiển và luật chơi

- Host chạy hai actor Player thật, Physics2D, AI, sát thương, PCG, portal và tiến trình 10 level.
- Client chỉ gửi input (di chuyển, Fire, Shift/chuột phải để Dash, Space, Q/E/R) và yêu cầu Pause/Resume. Không nhận vị trí, máu, sát thương hoặc điều kiện thắng từ Client.
- Đánh thường/giữ chuột và Space được xử lý qua controller hiện có trên Host. Mỗi actor có cooldown, HP, attack point và hiệu ứng riêng.
- Quái và boss chọn người còn sống gần nhất. Không có friendly fire; va chạm Player/Player bị bỏ qua.
- Hết máu: ngừng điều khiển; đồng đội vẫn chơi. Sang level mới hồi sinh người đã gục. Hai người cùng gục: Game Over.
- Esc Pause/Resume cả phòng. Chỉ Host được Restart. Restart giữ socket, tạo lượt chơi mới, reset level/HP/kỹ năng, gửi lại map.
- Bất kỳ người còn sống nào đi vào portal đã mở trên Host đều có thể đưa cả đội sang level tiếp theo. Client không tự mở portal hoặc cấp quyền chuyển tầng.
- Host chờ người thứ hai trước khi bắt đầu. Khi Client rời, Host tạm dừng và có thể nhận người Join lại ở level hiện tại; người Join lại là actor mới, đầy máu. Host chết và Client rời thì Game Over. Host rời sẽ đưa Client về menu với thông báo mất kết nối.

## Đồng bộ

`LanTransport`: socket TCP, độ dài frame 32-bit little-endian + UTF-8 JSON; send/read trên worker, chỉ xử lý Unity API ở main thread. Giới hạn 4 MiB/frame, 128 frame/queue, một Client, connect timeout 5 giây; mất heartbeat 12 giây thì ngắt. Chặn input NaN/Infinity, vector vượt độ dài 1, sequence cũ và input khác lượt chơi/level. Input không về sau 0,5 giây được xóa để tránh kẹt phím.

`LanAssetCatalog`: danh sách Sprite/Texture2D/TileBase/Material/AudioClip từ dependency scene và Resources, sắp theo định danh asset. Fingerprint bao gồm runtime code, input và package lock để kiểm tra hai build tương thích. Unity tạo asset/meta bằng Editor API; cập nhật trước build. Sprite VFX tạo bằng `Sprite.Create` được mô tả bằng texture ID/rect/pivot/PPU. Texture readable tạo lúc chạy (ví dụ frame VFX có padding) được mã hóa PNG, truyền lần đầu rồi cache tại Client; giới hạn 2048×2048 và 1 MiB/texture. Sprite đơn sắc dùng texture trắng nếu không có texture readable.

`LanWorldReplication`: map đầy đủ khi Join/chuyển level/Restart; snapshot toàn bộ SpriteRenderer đang hiển thị 15 Hz, spawn/despawn view tại Client. Đồng bộ Point Light2D của đuốc/boss (vị trí, tỷ lệ, màu, cường độ và bán kính); ánh sáng Player/darkness do session quản lý riêng. Vị trí view nội suy theo thời gian unscaled. View không có gameplay script/collider. Camera Client theo actor thứ hai; HUD máu/cooldown nhận số liệu Host. Âm thanh đánh/hit của Player và Enemy được gửi bằng event AudioClip ID, volume/pitch; nhạc nền scene hoạt động trên từng máy. Audio event tại Client dùng mix 2D, chưa có đồng bộ audio spatial riêng theo từng actor.

Không hỗ trợ WebGL vì dùng socket/thread. Bản đầu hướng tới Windows x64 và LAN có độ trễ thấp. Chưa có client prediction, discovery, mật khẩu phòng, NAT traversal, lobby Internet, host migration hay lưu phiên co-op. Cổng bị chiếm, sai IP, khác build và mất Host đều có thông báo về menu. Snapshot hình ảnh giữ gameplay nhất quán nhưng cần profiling/băng thông với nhiều VFX trước khi tăng số người/quái.

## Kiểm thử tái lập

- `LanTransportProbe.Run`: kiểm tra kết nối hai chiều UTF-8, config sai, đóng kết nối, Join lại, frame quá lớn và input edge/NaN.
- `LanBuildTools.BuildWindows`: cần biến môi trường `LABYRINTH_LAN_BUILD` trỏ tới file `.exe` ngoài Assets.
- Hai bản Development chạy `-lanHost -lanPort 28777 -lanSmoke` và `-lanJoin 127.0.0.1 -lanPort 28777 -lanSmoke` để thực hiện kiểm thử hai tiến trình. Probe kiểm tra movement qua socket, Q/E/Space, Pause/Resume, chuyển level, Restart, knockout, Game Over và Victory sau 10 level. Fixture đóng băng AI để kiểm tra luồng mạng ổn định; không phải kiểm thử cân bằng gameplay.
- `LanSmokeProbe` chỉ tồn tại trong Editor/Development Build và chỉ chạy khi có `-lanSmoke`; không đưa test vào bản Release.
- Kiểm thử loopback hai tiến trình không chứng minh Firewall/router và cảm giác chơi trên hai máy LAN thực tế. Cần chạy cùng build trên hai máy để nghiệm thu mạng LAN và kiểm tra hình ảnh/VFX/độ trễ.

## Lịch sử kiểm tra ngày 12/10/2026

Đã sửa lỗi dùng nền `31d8fcc` cũ khiến bản LAN thiếu nội dung UI/Enemy đã có trên `main`. Bản tích hợp hiện ghép nội dung `main` tại `416db1a` với LAN, giữ thay đổi scene/font/DOCX cục bộ; chưa commit hoặc push.

- Unity 2022.3.10f1 biên dịch và build Windows x64 Development thành công.
- EditMode: 8/8 Passed.
- PlayMode `RestoredContentProbe.Run` (D3D11): PASS minimap với RenderTexture, 5 tim và SpriteRenderer chỉ hướng quái trong chơi đơn.
- Hai tiến trình Windows bản cuối (D3D11), Host/Client nối qua `127.0.0.1:28777`: Host PASS hai actor, minimap, 5 tim, chỉ hướng quái, input Client, Pause/Resume, portal đủ 10 level, thu thập chìa khóa, Restart, một người gục vẫn chơi tiếp, hai người gục Game Over và Victory. Client PASS, nhận 178 snapshot, xác nhận HUD, Q/E/Space, Pause, chuyển tầng, Restart, Game Over, Victory và ba điểm màu đúng của texture runtime truyền bằng PNG.
- Chơi đơn: `CoreProgressionProbe` PASS 10/10 level, Pause/Resume, Game Over, Restart, Victory, portal animation và PowerUp VFX animation.
- Bản build kiểm thử: `C:\tl-lan-artifacts-20261012\build\TheLabyrinth.exe`; cần sao chép **toàn bộ thư mục build** sang máy thứ hai.
- Bằng chứng kiểm thử bản phục hồi: `D:\tl-lan-repair-20261012\` (`editmode.xml`, `restored-content-d3d.log`, `host-final.log`, `client-final.log`, `solo.log`, `build-clean.log`). Bản build cuối thành công, không có lỗi shader hoặc lỗi biên dịch C#. Đã đối chiếu hash toàn bộ 271 file khi bàn giao build. Kết quả transport 9/9 trước đó nằm trong `C:\tl-lan-artifacts-20261012\transport.log`, không phải lượt chạy lại sau tích hợp.

PlayMode chơi đơn và hai tiến trình player cuối chạy D3D11; các lượt player trước đó dùng `-nographics`. Đây là kiểm tra tự động sự hiện diện/cấu hình và luồng gameplay, chưa phải nghiệm thu hình ảnh bằng mắt hoặc hai máy LAN vật lý. Lượt Editor `-nographics` bị native crash trong render URP; chạy lại D3D11 PASS. Build đầu gặp shader compiler thiếu tài nguyên và lưu lỗi trong cache; đã chuyển cache của worktree kiểm thử sang thư mục backup, build lại với cache sạch và một shader compiler: PASS, không có lỗi shader. Các lượt PASS không có C# exception/assertion thất bại; còn cảnh báo AudioSource trong quá trình Awake. Kiểm thử chưa chứng minh cảm giác điều khiển, độ ổn định Firewall/router hoặc cân bằng co-op.

Project gốc vẫn ở nhánh `feature/core-levels-integration`; nội dung mới chưa commit/push. Sao lưu file trước phục hồi tại `D:\tl-lan-repair-20261012\root-preapply\` và `C:\tl-lan-repair-20261012\backup\`. Bản chạy cũ đã được chuyển sang `D:\tl-lan-repair-20261012\previous-build\`, có thể khôi phục. Font và DOCX cục bộ được giữ nguyên.

## Chẩn đoán không Join được từ máy khác

- Lượt kiểm tra này Host Wi-Fi là `10.154.6.73`; `192.168.137.1` thuộc Wi-Fi Direct/hotspot, không phải adapter Wi-Fi chính. IP có thể đổi; dùng IP được menu hiển thị hoặc IPv4 Wi-Fi hiện tại trong `ipconfig`, cổng mặc định `7777`.
- Menu ưu tiên IPv4 của adapter đang hoạt động có gateway và cập nhật định kỳ, thay vì địa chỉ DNS có thể cũ. Lỗi kết nối hiển thị IP/cổng đã thử và phân biệt timeout/không có Host lắng nghe.
- Windows Firewall hiện có rule chặn Unity Editor trên mạng Public; bản EXE ở đường dẫn trên có rule cho phép Public. Không tắt Firewall. Nếu Host bằng Editor hoặc EXE tại đường dẫn mới, người dùng cần cho phép đúng ứng dụng trên loại mạng đang dùng; code không sửa cấu hình hệ thống.
- Hai máy phải dùng cùng toàn bộ thư mục build mới, không ghép EXE mới với thư mục `_Data` cũ.

Trong Unity đang mở, nhấn **Ctrl+R** để import/recompile trước khi Play; nếu đổi asset khi thử Editor với một bản build, Refresh LAN Catalog và build lại để dùng cùng nội dung. Unity MCP tại `127.0.0.1:8080` chưa chạy ở lượt kiểm tra này nên kiểm thử tự động được thực hiện bằng Unity batchmode trong worktree riêng; không lưu lại scene/font/DOCX đang chỉnh của người dùng.

Không commit build, Library, log kiểm thử hoặc file save phát sinh.

## Tích hợp ngày 16/10/2026

Theo yêu cầu của chủ project, module LAN được đưa lên cùng bản merge combat/decor mới. Xem [báo cáo tích hợp](INTEGRATION_20261016.md) để biết phạm vi và kết quả kiểm chứng mới; các số liệu ở mục 12/10 là lịch sử, không thay thế lượt nghiệm thu này.

Host vẫn quyết định sát thương và trạng thái đội. Giới hạn nửa tim/đòn, bất tử sau trúng đòn, Q tự ngắm, Q/E cộng nộ và R đầy nộ khi bắt đầu/Restart được ghép với input từ Client. Texture procedural của boss được truyền bằng PNG thay vì thay bằng hình trắng.
