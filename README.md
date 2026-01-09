# Simple Google Drive Sync Tool

这是一个基于 **WPF** 框架构建的简单Google 云端硬盘同步工具，可以一键同步本地文件夹和云端文件夹。

<img width="800" height="450" alt="image" src="https://github.com/user-attachments/assets/7c35075e-b04a-4494-99cd-722b396d1cf4" />





> ⚠️ Important
>
> 出于 Google API 的安全策略及隐私保护，本开源项目源码**不包含**`client_secret.json` 私钥文件。
> 如果要运行此源码，须自行申请 Google API 凭证。

---

## 申请Google API凭证 (必需)

在运行代码前，请务必按照以下步骤配置 Google API 凭证：

1.  前往 [Google Cloud Console](https://console.cloud.google.com/) 并创建一个新项目。
<img width="2560" height="1317" alt="image" src="https://github.com/user-attachments/assets/e864a7ec-e0d9-47cd-8fa7-534fd5c71865" />

2.  搜索并启用 **Google Drive API**。
<img width="715" height="676" alt="image" src="https://github.com/user-attachments/assets/3b37bcd9-a12b-4fdf-b2b9-00af4bd68689" />


3.  前往“凭证 (Credentials)”页面，点击“创建凭证”并选择 **OAuth 2.0 客户端 ID** (应用类型选择 Desktop App)。
<img width="2559" height="1319" alt="image" src="https://github.com/user-attachments/assets/408aea4a-a945-49b8-9994-6b707e029a8b" />

<img width="2560" height="320" alt="image" src="https://github.com/user-attachments/assets/6a28050c-e5f7-4c96-8a27-d4a20312713f" />

4. 前往“目标对象”页面，将自己的Google账号添加到测试账户中，或者直接发布应用。
<img width="2560" height="1317" alt="image" src="https://github.com/user-attachments/assets/43017289-4708-48f3-a5cc-87d443ff9407" />

4.  下载生成的 JSON 凭证文件，并将其重命名为 `client_secret.json`。
<img width="914" height="274" alt="image" src="https://github.com/user-attachments/assets/d9707b9d-7787-4ce1-b3c0-d22166f818c7" />

5.  将 `client_secret.json` 文件复制到项目的 **运行目录** 或 **项目根目录** 下。

完成以上步骤后，程序即可正常启动。由于项目秘钥是用自己的Google账号创建的，不会有人通过此软件看到你Google硬盘上的隐私信息。

---
## 使用

### 1. 账号登录
程序首次启动时，会自动调用系统默认浏览器弹出 Google 登录页面。
* **登录成功**：程序界面上的“登录”按钮会自动消失，可以开始操作。
* **登录失败**：如果关闭网页或拒绝授权，工具将无法运行。点击界面上的登录按钮重试。

### 2. 配置同步路径
在主界面设置同步源与目标：

* **本地路径 (左侧)**：输入电脑上需要同步的文件夹路径（例如：`D:\MyFiles`）。
* **云端路径 (右侧)**：输入 Google 云端硬盘的目标文件夹网址 (URL)。
* **管理同步列表**： * 点击 **+** 和 **-** 按钮，添加和删除文件行。
    
* <img width="50" height="50" alt="image" src="https://github.com/user-attachments/assets/e00736ac-7b1c-4277-af99-0b7e3527e677" /> **保存配置**：点击 **Save** 按钮。
    * *配置信息将保存至本地，下次启动程序时自动加载。*


### 3.  同步

用 **MD5 哈希校验** 算法来精准检测文件差异。

* **Include Subfolders (子文件夹)**：
    * **勾选**：递归同步该文件夹下的所有子文件夹及文件。
    * **不勾选**：仅同步根目录下的文件，忽略子文件夹。

### <img width="50" height="50" alt="image" src="https://github.com/user-attachments/assets/e1e736da-9bfe-438e-8689-8d56a8e13e1a" /> Upload (上传)
**逻辑：本地 ➔ 云端**
*以本地文件为准，强制将云端状态修改为与本地一致。*

| 文件状态| 执行操作 |
| :--- | :--- |
| **MD5 值不同** | **覆盖**：删除云端旧文件，上传本地新文件 |
|  **云端缺失** | **新增**：直接上传本地文件至云端 |
|  **本地缺失** | **删除**：删除云端对应的多余文件 |

### <img width="50" height="50" alt="image" src="https://github.com/user-attachments/assets/1dcb07f4-6d11-4183-a5ea-c7770b71c53b" /> Download (下载)
**逻辑：云端 ➔ 本地**
*以云端文件为准，强制将本地状态修改为与云端一致。*

| 文件状态对比 | 执行操作 |
| :--- | :--- |
| **MD5 值不同** | **覆盖**：删除本地旧文件，下载云端新文件 |
| **云端缺失** | **删除**：删除本地对应的多余文件 |
| **本地缺失** | **新增**：直接下载云端文件至本地 |

#### 进度条会显示同步进度，等待同步完成即可。

---
