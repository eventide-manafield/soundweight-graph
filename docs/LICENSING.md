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
| FFmpeg / ffprobe | LGPL-3.0-or-later, LGPLv3와 그에 포함되는 GPLv3 전문 확보. 정확한 소스·빌드 레시피·검증한 의존성 캐시 및 고지 묶음 제공 |

`runtimeconfig.json`의 Windows Desktop 버전 표기와 실제 deps 런타임 팩 버전이 다르다.
`Environment.Version=10.0.11` 확인만으로 모든 데스크톱 네이티브 구성요소가 10.0.11이라고 판단하지 않는다.
차후 런타임 정리 시 deps, 실제 번들 파일 버전, 라이선스 목록을 함께 갱신해야 한다.

## FFmpeg 소스 및 배포 자료

내장 도구는 BtbN의 수정하지 않은 `n8.1.3-23-g5c4f5744cd-20261022` Windows x64 정적 LGPL 빌드다.
`--enable-version3`가 있고 `--enable-gpl` / `--enable-nonfree`는 없다. 앱은 별도 프로세스로 실행한다.
FFmpeg 소스, 정확한 빌드 레시피, 해당 빌드 실행의 전체 의존성 소스 캐시를 확보하고 원본 SHA-256을 검증했다.
129개 소스 아카이브에서 551개 고유 라이선스/저작권 텍스트를 수집했다. nv-codec-headers는 각 헤더의 원문 고지를 포함한다.
캐시에는 다른 플랫폼·빌드 변형과 빌드 도구도 들어 있으므로, 고지 전체가 이 Windows 실행 파일의 링크 라이브러리 목록을 뜻하지는 않는다.
`libdrm`은 현재 configure에서 비활성화되어 있으며 원본 소스는 캐시에 보존한다.

[소스 보관 및 재빌드 안내](FFMPEG_SOURCE_STATUS.md)와 [캐시 목록](ffmpeg-source-cache-inventory.json)에 경로와 해시를 기록한다.
같은 [v0.1.0 릴리스](https://github.com/eventide-manafield/soundweight-graph/releases/tag/v0.1.0)에 소스 키트와 캐시 3개 분할 파일을 제공한다.
원문 파일과 경로는 소스 키트의 `ffmpeg-dependency-notices.zip`으로 보존한다. 소스 자료는 EXE 안에 넣지 않는다.

`SOUNDWEIGHTGRAPH_FFMPEG_DIR`에 절대 경로를 지정하면 수정한 ffmpeg.exe와 ffprobe.exe를 덮어쓰지 않고 사용한다.
파일이 빠지면 오류를 표시하며 기본 내장 도구의 무결성 검사는 유지한다.
고지와 소스의 수집·검증을 수행했으며, FFmpeg의 전체 재빌드나 비트 단위 재현성 검증은 수행하지 않았다.
참고: [FFmpeg 재배포 안내](https://ffmpeg.org/legal.html), [LGPLv3 원문](licenses/upstream/FFmpeg-COPYING.LGPLv3.txt).

## Microsoft SDK 및 고지 전달

공식 [REDIST 목록](https://learn.microsoft.com/en-us/legal/windows-sdk/redist)의 Microsoft.Windows.SDK.NET.Ref에
`lib/net8.0/Microsoft.Windows.SDK.NET.dll`과 `WinRT.Runtime.dll`이 명시되어 있다.
현재 두 배포 DLL은 NuGet 원본과 SHA-256이 같다. [검증 기록](windows-sdk-redist-verification.json)을 보존한다.

Microsoft SDK 약관은 해당 구성요소에 그대로 적용한다. [외부 구성요소 이용 조건](../THIRD_PARTY_TERMS.md)에 범위와
최종 사용자/배포자 조건을 명시하고 릴리스 다운로드 페이지에서 안내한다. 앱 코드의 Apache-2.0 권한은 추가 제한하지 않는다.
프로젝트 LICENSE·NOTICE, Microsoft 원문 및 외부 고지 전문은 앱의 **오픈소스 고지**에도 내장한다.
이 기록은 수집·확인한 배포 자료를 설명하며 법률 검토 인증을 의미하지 않는다.

## 다시 수집하기

```powershell
# 개발용 네트워크 접근. 앱 실행 시 수행하지 않는다.
./scripts/fetch-license-sources.ps1
python ./scripts/collect-notices.py
```

Python 3 표준 라이브러리와 Windows PowerShell의 RTF 읽기를 사용한다.
누락된 패키지 고지가 있으면 수집기는 실패한다. 성공은 고지 파일 수집을 의미하며 법적 배포 조건 전체의 자동 승인을 의미하지 않는다.

