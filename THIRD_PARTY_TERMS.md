# 외부 구성요소 이용 조건

SoundWeight Graph에서 직접 작성한 코드는 [Apache-2.0](LICENSE)으로 제공한다.
이 문서는 해당 코드의 사용·수정·재배포 권리를 추가로 제한하지 않는다.

배포 실행 파일에는 Microsoft.Windows.SDK.NET.Ref 패키지의 수정하지 않은
`Microsoft.Windows.SDK.NET.dll`과 `WinRT.Runtime.dll`이 포함된다.
이 두 구성요소에는 [Microsoft Windows SDK 약관](docs/licenses/upstream/WindowsSDK-license.txt)이 적용된다.
권위 있는 원문은 같은 폴더의 [RTF](docs/licenses/upstream/WindowsSDK-license.rtf)다.

Microsoft 구성요소가 포함된 실행 파일을 다운로드·사용·재배포하기 전에 해당 약관을 읽고 동의해야 한다.
동의하지 않는 경우 해당 구성요소를 사용하거나 배포하지 않는다.
외부 배포자도 Microsoft 구성요소를 수정하지 않고, 고지를 보존하며,
Microsoft를 최소한 해당 약관과 동일한 수준으로 보호하는 조건을 최종 사용자와 배포자에게 적용해야 한다.
이 안내는 Microsoft 원문 조건을 대체하거나 Microsoft의 권리를 제한하지 않는다.

FFmpeg에는 LGPL-3.0-or-later가, 다른 외부 라이브러리에는 각각의 원래 라이선스가 적용된다.
FFmpeg와 그 라이브러리의 수정·재빌드·교체 및 해당 라이선스가 허용하는 역공학을 금지하지 않는다.
앱은 별도 FFmpeg 도구 폴더 지정 기능을 제공한다. 대응 소스와 빌드 자료는 같은 릴리스의 별도 첨부로 제공한다.

전체 원문과 크레딧은 [외부 소프트웨어 고지](docs/THIRD_PARTY_NOTICES.md) 및 앱의 **오픈소스 고지**에서 확인할 수 있다.
