# 分类补图与图片显示 TDD 验证

本报告记录 v1.0.3 本地预览阶段的图片修复验证；这些修复随后合并至 v1.0.4。

## 范围

使用 D 盘隔离访客目录、可控平台适配器和本地 HTTP 图片服务，不读取日常账号。真实 CategoryPage、CategoryVM、目录缓存和 DecodedImage 均参与运行，不替换这些内部组件。

## 失败用例与修正

新增用例最初发现图片已具有尺寸甚至可以采样到实际图像，但占位提示未收起。图片尺寸仅代表已读取图片信息，因此测试进一步检查 RenderTargetBitmap 的实际 BGRA 像素，以及占位是否消失。

共享图片加载器现在维护明确的完成状态：同时响应 Image 控件与 BitmapImage 的完成/失败事件；更换来源或卸载时清空状态、解除旧图片事件。分类占位绑定这一状态，移除页面上分散的完成/容器回调。仅监听 BitmapImage 时曾复现“像素可绘制、完成状态未更新”；补齐控件事件后完整用例通过。

这些证据覆盖本地可控条件下的时序问题，不能据此断言用户此前所有远程图片失败均由同一个原因造成。

## 通过的检查

- 同名游戏补图进入实际分类卡片，原抖音分类 ID、父 ID、平台和导航来源保持不变。
- 本地 HTTP PNG 经实际解码后，卡片中心渲染像素符合预先设定的颜色，且占位收起。
- 页面离开释放图片引用；返回后重新显示真实图片，并复用已成功的目录缓存。
- 页面取消传播到底层补图请求，离开后不写入迟到结果；返回后允许重试并完成补图。
- 第一补图来源失败后使用后备平台，同名匹配仍保持抖音分区。
- 现有离线数据检查通过：含同名匹配、无匹配、无效地址、保留已有图和数据保存等断言。
- 现有 WinUI performance-checks 通过：目录缓存及取消、图片尺寸与回收、WebView2 生命周期、关注取消、随机导航和浏览偏好。

## 证据

- 新增入口：`PlayWin.exe --smoke-test --image-checks`。
- 完整图片检查：`artifacts/cache/temp/PlayWin-smoke-test/tdd-image-final-fdd8ea8818c84974acb402e060ad01c1/category-image-checks.log`。
- 既有回归：`artifacts/cache/temp/PlayWin-smoke-test/tdd-regression-535d3b6e3e5e4fe5a149cac4900d24e6/performance-checks.log`。
- 可运行程序：`artifacts/PlayWin-preview/PlayWin.exe`。

重新运行时通过 `with-build-cache.ps1` 设置 D 盘缓存，并将 `PLAYWIN_SMOKE_PROFILE` 指向 D 盘 TEMP/PlayWin-smoke-test 下的新目录；离线数据检查使用 `PLAYWIN_TEST_DIR`。

## 未验证项

未使用日常登录会话，未声称实测各平台当前 CDN 可用性、防盗链或所有游戏的封面覆盖率。无同名有效封面的分类仍显示占位；本轮没有增加应用层磁盘图片缓存，也未执行长时间性能基准。

## v1.0.4 发布前截图补查

真实分类截图暴露透明图标下方残留“暂无图片”。新增失败用例确认：已有有效图片来源时，占位仍在等待原生完成事件。占位改为表达“缺少来源／加载失败”，有效来源开始加载时隐藏，加载失败或清空时恢复，不再将完成事件作为隐藏占位的唯一条件；状态命名同步改为 HasPicture。图片是否真正解码依然通过独立的渲染像素断言验证，不能用 HasPicture 替代解码证据。

发布前新增用例：`release-placeholder-red-2959715e177f40faa4d73fcb1082e30a` 失败，修复后 `release-placeholder-green-8a001d7325bb4633bc082b573e1399b9` 完整图片检查通过；更新后的访客真实分类截图中透明游戏图标不再叠加缺图提示。
