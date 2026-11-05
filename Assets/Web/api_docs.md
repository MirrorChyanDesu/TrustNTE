# TrustNTE 本地 API 接口文档
基础地址：http://localhost:8888

## 1. 启动截图器
- 接口：`GET /api/v1/start`
- 返回：`{ "code": 200, "msg": "started" }`

## 2. 停止截图器
- 接口：`GET /api/v1/stop`
- 返回：`{ "code": 200, "msg": "stopped" }`

## 3. 获取运行状态
- 接口：`GET /api/v1/status`
- 返回：`{ "code": 200, "isRunning": true/false, "hwnd": "句柄" }`

## 4. 获取近期日志
- 接口：`GET /api/v1/logs`
- 返回：`{ "code": 200, "logs": ["日志1", "日志2"] }`

## 5. 获取实时画面 (Base64 JSON)
- 接口：`GET /api/v1/screenshot_base64`
- 返回：`{ "code": 200, "width": 1920, "height": 1080, "imageBase64": "..." }`

## 6. 获取实时画面 (直接返回图片流)
- 接口：`GET /api/v1/screenshot`
- 返回：`image/jpeg` 二进制流

## 7. 网页端实时监控
- 接口：`GET /api/v1/viewer`
- 返回：带有画质与帧率选择器的 HTML 实时监控页面