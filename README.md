# GameProject

Starter cho một game offline chạy local trên máy bạn. Bộ khung này có:

- Server nội bộ bằng Node.js để phục vụ game files.
- Một màn hình demo dùng Canvas.
- Vòng lặp game tối thiểu để bạn mở rộng sang gameplay thật.

## Công cụ nên bắt đầu

Nếu bạn muốn làm game offline nhanh và dễ mở rộng, hãy bắt đầu với:

1. VS Code: để sửa code, debug, và quản lý project.
2. Node.js LTS: để chạy local server và công cụ build đơn giản.
3. Chrome hoặc Edge DevTools: để kiểm tra Canvas, FPS, và lỗi runtime.
4. Git: để lưu phiên bản và quay lại khi thử nghiệm cơ chế mới.
5. Một engine sau này nếu game lớn hơn: Godot, Unity, hoặc Phaser nếu bạn muốn làm game web.

## Cấu trúc dự án

```text
GameProject/
├─ README.md
├─ package.json
├─ server.js
└─ Main/
	├─ index.html
	├─ styles.css
	└─ game.js
```

## Chạy thử

```bash
npm install
npm start
```

Sau đó mở `http://localhost:3000`.

## Hướng mở rộng

- Thêm hệ thống nhân vật, quái vật, và item.
- Tách logic game ra thành nhiều module.
- Lưu dữ liệu cục bộ bằng file JSON hoặc SQLite nếu cần.
- Nếu muốn offline hoàn toàn, giữ toàn bộ asset và logic ở máy local.
