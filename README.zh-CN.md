[English](README.md) · [Deutsch](README.de.md) · **简体中文**

<img src="docs/logo.png" alt="SyncTClient" width="110" height="110" align="right">

# SyncTClient

> [!NOTE]
> 这是原 SyncTClient 的个人续作，已与原项目脱钩。版本号从 1.0.0 重新开始，
> 不再跟进原项目的改动。

[![Latest release](https://img.shields.io/github/v/release/ZXY-Baa/SyncTClient)](https://github.com/ZXY-Baa/SyncTClient/releases/latest)

一个面向 Windows 的 Syncthing 客户端：文件以**占位符**的形式呈现，内容在首次
访问时才传输——并按卷自行管理缓存。

文件在文件系统中仍然是普通文件。没有容器、没有虚拟磁盘、没有专有格式：客户端
挂载出来的内容，任何其他程序都可以同时读取。

> [!CAUTION]
> ## 不提供担保，不承担责任。使用风险完全由你自行承担。
>
> **本软件会传输、替换、释放和删除文件。** 它会更改操作系统设置、注册同步根，
> 并挂接文件管理器。
>
> **对于因安装、使用、故障或无法使用本软件而产生的或与之相关的任何损害，作者
> 不以任何法律依据承担任何责任**——包括本计算机以及与它交换数据的每一台其他
> 设备上的数据丢失、删除、覆盖或损坏，数据泄露给对端，系统上残留的更改，利润
> 损失、业务中断以及数据恢复费用。即使已提示过此类损害的可能性，本条仍然适用。
> **特此明确拒绝任何及全部损害赔偿请求。**
>
> **全部责任由用户独自承担。** 是否使用本软件、用于何种目的，仅由你决定；后果
> 也仅由你承担。**切勿在没有独立且经过验证的备份的情况下使用，也切勿将其作为
> 无法承受其丢失的数据的唯一存放处。**
>
> 完整条款：[使用条款](setup/EULA.zh-CN.txt) · 安装程序会以所选语言显示条款，
> 必须接受之后才能继续安装。

<img src="docs/interface.png" alt="SyncTClient 主窗口" width="1266" height="813">

*共享的状态、进度、积压与吞吐。展开一个共享后，可以看到对端持有什么、本地保留
了什么，以及已经通过网络传输了多少。*

## 为什么要做

现有的同步服务没有一家能同时做到下面四件事：

| | |
|---|---|
| a) 类似 OneDrive 的占位符 | Nextcloud、Seafile ✓ · Syncthing ✗ |
| b) 可设上限的自管缓存 | Seafile、rclone ✓ · Nextcloud ✗ |
| c) 同一局域网内点对点 | Syncthing、Resilio ✓ · Nextcloud ✗ |
| d) 挂载已有目录 | Syncthing、Resilio ✓ · Seafile ✗ |

Syncthing 原生做到了 (c) 和 (d)。本项目补上 (a) 和 (b)——作为一个独立节点，
说 Block Exchange Protocol。对面是一个未经修改的 Syncthing。

## 这不是空想

Syncthing 的全局索引为文件夹里的**每一个**文件携带名称、大小和完整块列表——
包括尚未在本地落地的文件。而 BEP 有一条 `Request` 消息，参数是
*(folder, name, offset, size, hash)*：可以在网络上随机取块。

所以占位符背后的目录已经存在，而提供文件内容只是一次定点块请求，不必整体拉取。

### 诚实公告是协议自带的

Syncthing 里的 `FileInfo.setLocalFlags()` 会调用 `setNoContent()`，把块列表和
大小丢弃。因此一个设备只公告它真正持有的内容——对端永远不会向我们索取我们没有
的数据。这一条不需要绕开，而是可以拿来用。

## 客户端做什么

### 占位符

- 通过 Cloud Filter API 在资源管理器中呈现占位符；有人打开文件时才传输内容
- 覆盖图标——云朵、圆环、绿色对勾——由固定状态推导
- **每个文件、每个文件夹一个模式**，存在索引里：*占位符* 或 *完全保留在本地*。
  文件夹把模式向下传递，并覆盖其下的条目
- 缓存上限**按卷**，按最后访问时间淘汰，内容变化时失效
- 只在有证据时释放：只有当某个对端在它的索引里**完整**持有该内容（`Size > 0`
  且有块列表）时才释放本地副本。需要几个对端可以按共享配置
- 本地修改过的文件永不释放
- 每小时的扫描把文件系统中的固定属性与数据库对齐——在文件管理器里做的更改，
  之后会在这里生效

#### 什么时候一个文件才可以变成占位符

<img src="docs/placeholder-threshold.png" alt="占位符阈值设置" width="647" height="128">

丢弃文件内容是这里唯一可能丢失数据的操作，所以它的防护最多。内容绝不会仅仅因为
缓存满了就被丢弃，只有在文件被**证明**可恢复之后才会被丢弃。

阈值决定数量：必须有多少个其他节点**完整**持有该文件，它的字节才可以从这里消失。
默认为 1，这是对着单个服务器的常见情况。如果你不希望文件依赖另一台设备，就设为
2。不允许小于 1——网络中的最后一份副本不能消失。

计数是严格的。仅仅**知道**这个文件的对端不算数：它的索引条目必须带块列表，且
大小大于零。Syncthing 在不持有内容时公告里就没有块列表，所以诚实的计数是协议
自己给出的。

而且阈值是必要条件，不是充分条件。在哪怕一个字节离开之前，下面每一条都必须成立：

| 条件 | 原因 |
|---|---|
| 足够多的对端完整持有 | 即阈值本身 |
| 它们持有的是**这一版** | 按版本向量比较，而不是时间戳。时间戳只说明哪次修改更晚，不说明一方是否知道另一方的修改。这一条弄错，占位符下次访问就会取回旧版本——而且悄无声息，因为留下的文件本身合法，只是内容不对 |
| 公告与这份内容相符 | 名字本身不能证明任何事。对端完全可能在同一名字下持有另一个文件，而一份还没有任何人见过的本地修改会因此被删掉 |
| 块相符 | 是算出来的，不是假设的 |
| 公告足够旧 | BEP 从不告诉发送方“接收方已经完成”。更糟的是：对端会在真正取到任何一个字节之前很久，就带着块列表把**我们自己的公告**回声回来。把它当作持有证据，就会在数据到达任何地方之前删掉文件，所以客户端选择等待 |
| 文件未被固定、也不是*完全保留在本地* | 明确的指示优先于缓存上限 |
| 文件未被打开、本地未被修改 | Windows 本来也会拒绝；提前检查能让待处理的修改暴露出来，而不是静默失败 |

这适用于所有会丢弃内容的路径：右键菜单、卷视图、卷达到上限时的自动释放，以及
清空缓存的按钮。*释放空间* 是一个请求，不是授权。达到上限时最先被释放的，是最久
没有被碰过的内容。

空文件是一个例外：零字节的文件永远达不到阈值，因为计数要求有块，而它没有块。
当它在这里为空**且**对端也为空时才释放——判断依据是它自己的大小，而不是公告的
大小，因为对端对一个大文件也可能报告大小为零（它只是知道这个名字）。

### 传输

- C# 实现的 BEP：帧格式、Hello、设备 ID、索引、按块取数据、LZ4
- 自己的 TLS 层，因为 Windows 不支持 Ed25519
- **双向。** 客户端既接受共享，也提供自己的文件夹：Index 和 IndexUpdate 会发
  出去，入站连接也会接受
- 本地发现与发现服务器；地址是动态的对端也能找到
- 每个对端一条连接，承载该对端的所有文件夹；掉线的对端会自动重新接上
- 索引存在 SQLite 里并支持续传：重启后只收变更
- 冲突处理沿用 Syncthing 的模式，但用设备名代替短 ID：
  `name.sync-conflict-YYYYMMDD-HHMMSS-DEVICE.ext`
- 被替换和删除的版本放在 `.stversions`，保留期可配置；也可以走回收站
- 每个共享可以设置忽略模式

### 在文件管理器中

一个原生编译的 shell 扩展，注册在 `HKEY_CURRENT_USER` 下——不需要管理员权限：

| 条目 | 效果 |
|---|---|
| 始终保留在此设备上 | 模式变为*完全保留在本地*，内容会被传输 |
| 释放空间 | 丢弃内容，占位符保留 |
| 隐藏此文件夹 | 把该分支移出同步 |
| 作为共享提供…… | 把任意文件夹变成共享 |

条目会显示当前实际生效的状态，并且支持多选操作。

另外，缩略图是按需生成而不是预先下载：文件管理器请求图片时，客户端只传输文件
开头——一个 128 KiB 的块——并从中提取内嵌的 EXIF 预览图。实测：新图片中位数
42 ms，已缓存过的 2 ms。整个过程中文件始终是占位符。

### 用户界面

- 管理共享、接受提供的文件夹、解除绑定、子树选择、视图过滤
- 按卷的占位符管理，以树形展示，跨所有共享
- 传输进度、吞吐图表、双向积压
- 日志窗口、带状态角标的托盘图标
- 德语、英语和简体中文，浅色与深色主题
- 每天备份配置，包括设备证书
- 可选的新版本检查——从不、每次启动、每周或每月。它只是问 GitHub 哪个版本
  最新，别的什么都不做：不下载任何东西，请求以未认证方式发出，也不携带任何
  与本机相关的信息。有新版本时工具栏上方会出现提示，并带下载页面的链接

<img src="docs/directory-select.png" alt="子树选择" width="746" height="633">

*子树选择：哪些分支要留在本设备上。每个节点都带着文件数和大小，所以选择依据的
是数字而不是猜测。*

## 安装

现成的安装包在 [Releases](https://github.com/ZXY-Baa/SyncTClient/releases/latest)。

需要 Windows 10 2004（内部版本 19041）或更新版本，64 位。
**不需要管理员权限。** 程序会在首次启动时自行注册资源管理器集成（位于
`HKEY_CURRENT_USER` 下），并可以按要求移除。

### SmartScreen 会警告

<img src="docs/smartscreen.png" alt="Microsoft Defender SmartScreen 警告" width="531" height="497">

启动安装程序时，Microsoft Defender SmartScreen 会提示“Windows 已保护你的电脑”，
并把发布者标为未知。原因有两个，都不是问题：

- **没有代码签名证书。** 一张 Windows 信任的证书每年都要花钱。对一个只有我一个
  人编写、也不赚钱的程序来说，这不在考虑范围内。
- **SyncTClient 很新。** SmartScreen 也会按声誉判断：很少被下载的东西对它来说
  是陌生的，出于安全它会先警告。随着安装量增长，这个提示会逐渐消失，但需要
  时间。

要继续：点**更多信息**，再点**仍要运行**。

想确认下载到的就是这里发布的文件，可以把它和
[发布页](https://github.com/ZXY-Baa/SyncTClient/releases/latest)上给出的校验和
比对：

```powershell
Get-FileHash .\SyncTClient-*-setup.exe -Algorithm SHA256
```

如果两者一致，说明文件在传输途中没有被改动。但这不能证明文件是谁构建的——只有
签名才能做到这一点。

## 目录结构

```
src/SyncTClient.Bep/               协议库，与平台无关
  Protos/bep.proto                 来自 syncthing/proto/bep/bep.proto
  DeviceId.cs                      base32 + Syncthing 的校验位方案
  DeviceIdentity.cs                设备证书；ID = DER 的 SHA-256
  BepFraming.cs                    线路帧格式 + LZ4 块格式
  BepTls.cs                        TLS，因为 Windows 做不了 Ed25519
  HelloExchange.cs                 认证前的握手
  BepConnection.cs / BepListener   读取循环、请求/响应、入站
  LocalDiscovery / GlobalDiscovery 网络上的发现
  FolderIndex / PersistentFolderIndex   内存中的索引与 SQLite 中的索引
  FileFetcher.cs                   带哈希校验的按块传输

src/SyncTClient.Vfs/               Cloud Filter API
  WinRtSyncRoot.cs                 同步根注册、内容策略
  CloudFilterMount.cs              文件系统回调
  HydrationCache / CacheLimits     按卷的记账与释放

src/SyncTClient.Mount/             客户端：共享、同步、命令
src/SyncTClient.Gui/               WPF 用户界面
src/SyncTClient.ExplorerProvider/  shell 扩展，NativeAOT
src/SyncTClient.ThumbHost/         缩略图提供者的宿主进程
src/SyncTClient.Probe/             用于验证的控制台工具

setup/SyncTClient.iss              Inno Setup 脚本
tools/Veroeffentlichen.ps1         构建安装包并发布
```

## 从源码构建

需要 .NET 10 SDK，以及用于 NativeAOT 部分的“使用 C++ 的桌面开发”工作负载。

```
dotnet build SyncTClient.slnx -c Release
```

只支持 `win-x64`；其他任何 RuntimeIdentifier 都会报错退出。发布通过
`FolderProfile` 配置输出到 `BIN`，之后由
`tools\Veroeffentlichen.cmd` 把它变成安装包和发布版本。

控制台工具会打印对端的索引，**不向磁盘写入任何东西**：

```
dotnet run --project src/SyncTClient.Probe -- --id
dotnet run --project src/SyncTClient.Probe -- --addr 192.168.1.42:22000 --target <PEER> --folder <FOLDER>
```

## 状态

客户端正对着 Syncthing v2 日常使用。当前版本号在
[发布页](https://github.com/ZXY-Baa/SyncTClient/releases/latest)和上方的徽章上；
每一次改动都记在[变更记录](CHANGELOG.md)里。

还有一件事没做完：合并“完全保留在本地”时内容到达的两条路径。固定会让 Windows
去请求内容，而后台的一遍又通过一个旁路文件抓同一份文件。两者相遇时，Windows
会报告文件被锁定。

## 参与

这是独立的续作，不再跟随原项目。源码在这里，依据 Apache License 2.0 供阅读和
使用；出处见 [NOTICE](NOTICE)。有问题或建议欢迎提 issue；改动只在这里进行。

## 责任

无。参见本页开头的警告和完整的
[使用条款](setup/EULA.zh-CN.txt)。安装程序会把这些条款作为一页显示，必须
接受之后才会向磁盘写入任何东西。

## 许可证

Apache 2.0 —— 见 [LICENSE](LICENSE)。
