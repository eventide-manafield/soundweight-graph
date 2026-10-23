# SoundWeight Graph 라이선스 정리

확인일: 2026-10-23. 직접 작성한 코드의 라이선스는 사용자 선택에 따라 **Apache-2.0**으로 적용한다.
저작자 표기는 `eventide-manafield`, 원본 출처는 <https://github.com/eventide-manafield/soundweight-graph>다.

## 프로젝트 자체

- 루트 `LICENSE`: Apache Software Foundation의 Apache License 2.0 공식 원문.
- 루트 `NOTICE`: 프로젝트명, 저작자, 원본 저장소 주소와 외부 구성요소 고지 위치.
- 표시 이름은 SoundWeight Graph, 실행 파일과 코드 식별자는 SoundWeightGraph로 통일한다. README는 v0.1.0용 안내 문장과 라이선스 링크만 제공한다.
- NOTICE는 라이선스를 변경하는 추가 약관이 아니다. 메인 화면에 로고를 고정하거나 모든 광고에 이름을 표시하는 요구를 추가하지 않았다.

Apache-2.0 제4조에 따라 배포 시 라이선스 사본, 수정 파일의 변경 표시, 적용되는 저작권·출처 고지 및 NOTICE의 출처 고지 보존이 필요하다.
NOTICE는 라이선스가 허용하는 고지 파일·소스/문서·화면 등의 방식으로 전달할 수 있다.
참고: <https://www.apache.org/licenses/LICENSE-2.0>

## 실제 구성요소 조사

`packages.lock.json`과 Release win-x64의 `SoundWeightGraph.deps.json`, 로컬 NuGet 패키지의 nuspec/원문을 대조했다.
복원된 패키지와 실제 런타임 파일을 가진 패키지를 구분해 [dependency-inventory.json](dependency-inventory.json)에 기록한다.
원문 출처와 SHA-256은 [licenses/provenance.json](licenses/provenance.json)에 남긴다.

| 구성요소 | 확인한 조건 / 상태 |
|---|---|
| ScottPlot / ScottPlot.WPF 5.1.59 | MIT, nuspec의 커밋과 일치하는 공식 원문 확보 |
| SkiaSharp 3.119.0 계열 | MIT 및 패키지의 포함 라이브러리 고지 확보 |
| HarfBuzzSharp 8.3.1.1 계열 | MIT 및 네이티브 구성요소 고지 확보 |
| OpenTK 4.9.4 | MIT + OpenEXR 관련 BSD 고지 확보 |
| OpenTK.GLWpfControl 4.3.3 | 버전 태그의 MIT 원문 확보. 패키지 메타데이터는 구형 licenseUrl 형식 |
| OpenTK.redist.glfw 3.4.0.44 | 패키지의 zlib 형태 COPYING.md 원문 확보 |
| .NET Core runtime 10.0.11 | 패키지 LICENSE 및 THIRD-PARTY-NOTICES 확보 |
| Windows Desktop runtime | 현재 deps의 실제 런타임 팩 버전은 **10.0.3**. 패키지 LICENSE 확보 |
| Windows SDK .NET Ref 10.0.19041.57 | 패키지가 가리키는 Microsoft Windows SDK 약관 RTF와 읽기용 텍스트 확보. MIT로 일괄 표기하지 않음 |
| FFmpeg / ffprobe | LGPL-3.0-or-later, LGPLv3와 그에 포함되는 GPLv3 전문 확보. 대응 소스 묶음은 아직 미완료 |

`runtimeconfig.json`의 Windows Desktop 버전 표기와 실제 deps 런타임 팩 버전이 다르다.
`Environment.Version=10.0.11` 확인만으로 모든 데스크톱 네이티브 구성요소가 10.0.11이라고 판단하지 않는다.
차후 런타임 정리 시 deps, 실제 번들 파일 버전, 라이선스 목록을 함께 갱신해야 한다.

## FFmpeg에서 남은 작업

현재 바이너리는 BtbN의 `n8.1.3-23-g5c4f5744cd-20261022` Windows x64 정적 LGPL 빌드다.
configure 기록에 `--enable-version3`가 있고 `--enable-gpl` / `--enable-nonfree`는 없다.
FFmpeg 자체 출력은 LGPL 3 또는 이후 버전을 명시한다.
앱은 FFmpeg를 별도 프로세스로 실행하며 libav 라이브러리에 직접 링크하지 않는다.
**이 구분은 앱의 라이선스 판단에 중요하지만, 포함한 FFmpeg 실행 파일의 재배포 조건을 없애지는 않는다.**

공개 실행 파일 배포 전에 다음을 해결한다.

1. 실제 바이너리에 대응하는 FFmpeg 소스, 패치, 빌드 설정 및 필요한 정적 링크 의존성 소스를 확보한다.
2. 해당 소스로 빌드할 수 있는 스크립트와 버전 정보를 함께 보관하고, 추가 라이선스·고지·재링크 관련 조건을 각 구성요소에 맞게 확인한다.
3. 바이너리 배포 페이지에서 대응 소스를 받을 수 있는 실제 경로를 제공하고 유지한다. upstream 홈페이지나 커밋 링크만으로 완료 처리하지 않는다.
4. 각 정적 링크 라이브러리의 라이선스 원문과 배포 조건을 조사한다. configure 옵션 목록은 실제 포함 코드의 완전한 목록이 아니다.
5. 변경본 도구 사용 경로는 구현했다. `SOUNDWEIGHTGRAPH_FFMPEG_DIR`에 절대 경로를 지정하면 그 폴더의 ffmpeg.exe와 ffprobe.exe를 덮어쓰지 않고 사용한다. 기본 내장 도구의 무결성 검증은 유지한다. 대응 소스 및 정적 링크 구성요소의 조건 충족은 별도 확인 항목이다.

이번에 확인한 소스 식별자:

- FFmpeg: `5c4f5744cd60943125a78db692b728210102f255`
- BtbN 날짜 태그의 빌드 레시피: `937868360a21d711ec94a33f5591d345ad9b7754`

이는 확보해야 할 소스를 식별하기 위한 정보이며 완전한 대응 소스 아카이브가 아니다.
빌드 레시피의 외부 Git 분기/태그가 가리켰던 실제 버전까지 확인해야 한다.
필요 없는 라이브러리가 적은 전용 FFmpeg 빌드를 만드는 방법도 있지만, 현재 바이너리를 조용히 교체하지 않았다.
참고: [FFmpeg 재배포 안내](https://ffmpeg.org/legal.html), [GPLv3 제6조](https://opensource.org/license/gpl-3.0),
[LGPLv3 원문](licenses/upstream/FFmpeg-COPYING.LGPLv3.txt).

## 기타 배포 확인

- Windows SDK 파일 매핑 확인 완료: [Microsoft 공식 REDIST 목록](https://learn.microsoft.com/en-us/legal/windows-sdk/redist)의 Microsoft.Windows.SDK.NET.Ref 항목에 lib/net8.0의 `Microsoft.Windows.SDK.NET.dll` 및 `WinRT.Runtime.dll`이 명시되어 있다. 현재 패키지와 배포 출력의 두 파일 SHA-256을 대조한다.
- SDK 약관 2.a.ii에는 외부 최종 사용자/배포자에게 Microsoft를 보호하는 조건에 동의하도록 하는 요구가 있다. 재배포 가능 파일 확인과 최종 배포 약관 적용은 별개이며, 공개 배포 전 적용 방식을 확정해야 한다. Apache-2.0을 Microsoft DLL에 대신 적용하지 않는다.
- 원본 BtbN 아카이브와 소스 제공 경로를 별도로 보존한다. 날짜 태그는 영구 보존을 보장하지 않는다.
- 프로젝트 LICENSE·NOTICE와 외부 고지는 빌드 시 앱의 ‘오픈소스 고지’ 리소스에 포함된다.
- 저장소에 소스를 공개하는 것과 외부 바이너리를 포함한 EXE를 공개 배포하는 것을 분리해 판단한다.

현재 판정: **프로젝트 라이선스 적용·고지 수집은 완료, 공개 바이너리 재배포 점검은 미완료**.

## 다시 수집하기

```powershell
# 개발용 네트워크 접근. 앱 실행 시 수행하지 않는다.
./scripts/fetch-license-sources.ps1
python ./scripts/collect-notices.py
```

Python 3 표준 라이브러리와 Windows PowerShell의 RTF 읽기를 사용한다.
누락된 패키지 고지가 있으면 수집기는 실패한다. 성공은 고지 파일 수집을 의미하며 법적 배포 조건 전체의 자동 승인을 의미하지 않는다.

