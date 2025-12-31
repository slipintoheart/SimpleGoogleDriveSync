# Simple Google Drive Sync Tool (WPF)

这是一个基于 WPF 框架构建的简单、暴力的 Google 云端硬盘同步工具。
本项目演示了如何通过 Google Drive API 进行文件的上传、下载及同步操作，**仅供学习和参考**。

<img width="786" height="443" alt="image" src="https://github.com/user-attachments/assets/0fdd45bc-9f84-42bb-94a7-c5fd962f1c4c" />


由于 Google API 的安全限制，本项目**不包含**私钥文件。如果你想运行此源码，必须自行申请 Google API 凭证。

1. 请前往 [Google Cloud Console](https://console.cloud.google.com/) 创建项目。
2. 启用 **Google Drive API**。
3. 创建 OAuth 2.0 客户端 ID，并下载 JSON 凭证文件。
4. 将下载的文件重命名为 `client_secret.json`。
5. 将 `client_secret.json` 放入项目的运行目录或根目录下，程序才能正常启动。

## 使用

### 1. 账号登录

<img width="200" height="203" alt="image" src="https://github.com/user-attachments/assets/a8590958-714e-499e-bc55-dd9d50650e06" />

* 程序首次启动后，会自动弹出默认浏览器窗口，要求你登录 Google 账号并授权。
* **登录成功**：程序界面上的“登录”按钮会消失。
* **登录失败**：如果关闭了网页或授权失败，工具将无法使用。你可以点击界面上的按钮尝试再次登录。

### 2. 配置同步路径
* **左侧输入框**：填写本地电脑上想要同步的文件夹路径（例如：`D:\MyFiles`）。
* **右侧输入框**：填写 Google 云端硬盘的目标文件夹网址（URL）。
* **保存配置**：点击 **Save** 按钮，上述路径信息会被保存到本地，下次打开程序时会自动加载。
  
  <img width="767" height="88" alt="image" src="https://github.com/user-attachments/assets/72a04992-7e62-4832-834f-32077449a51e" />


##  同步逻辑

本工具采用 **MD5 校验** 方式来检测文件差异，并“暴力”同步。

<img width="300" height="156" alt="image" src="https://github.com/user-attachments/assets/b9b5319b-687f-431b-9e2e-f258af68d6f8" />

### 📤 点击 Upload (本地 ➔ 云端)
*以本地文件为准，强制将云端状态同步为与本地一致：*

| 文件状态 | 执行操作 |
| :--- | :--- |
| **MD5 值不同** | 删除云端旧文件，上传本地新文件 |
| **云端缺失** | 直接上传本地文件 |
| **本地缺失** | 删除云端对应的文件 |

### 📥 点击 Download (云端 ➔ 本地)
*以云端文件为准，强制将本地状态同步为与云端一致：*

| 文件状态 | 执行操作 |
| :--- | :--- |
| **MD5 值不同** | 删除本地旧文件，下载云端新文件 |
| **云端缺失** | 删除本地对应的文件 |
| **本地缺失** | 直接下载云端文件 |

##  开发环境
* IDE: Visual Studio
* Framework: .NET Framework (WPF)

##  License
本项目开源仅供参考。
