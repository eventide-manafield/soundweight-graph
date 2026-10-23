# FFmpeg 소스 보관 및 재빌드 안내

내장 도구는 BtbN `n8.1.3-23-g5c4f5744cd-20261022`의 수정하지 않은 Windows x64 정적 LGPL 실행 파일이다.
앱은 별도 프로세스로 통신한다. 앱 코드는 Apache-2.0, 도구는 LGPL-3.0-or-later 조건을 유지한다.

## 제공 자료

[v0.1.0 릴리스](https://github.com/eventide-manafield/soundweight-graph/releases/tag/v0.1.0)의
`SoundWeightGraph-0.1.0-third-party-sources.zip`과 `ffmpeg-dependency-sources.zip.001`~`.003`을 함께 받는다.
일반 프로그램 실행에는 이 소스 자료가 필요하지 않다.

| 자료 | 식별자 | SHA-256 |
|---|---|---|
| FFmpeg 소스 | `5c4f5744cd60943125a78db692b728210102f255` | `C6978FA52D470FF3E3E5E96B54F51155580B61FFFC32447304189719B281B522` |
| BtbN 레시피 | `937868360a21d711ec94a33f5591d345ad9b7754` | `3D782F1B71B4F6CEF89C9E0014176542D071B1DFBF5DE41B9F96AAC6A442B1AF` |
| 의존성 캐시 ZIP | 2,210,375,345 bytes | `F89C6C43969D92C8B95CB459A90FBAFBC743D3D3CC4D16C610317F9CEEF922F8` |

캐시 출처: [공식 빌드 실행 39776938430](https://github.com/BtbN/FFmpeg-Builds/actions/runs/39776938430),
artifact `download-cache` / `12424698518`. GitHub가 기록한 전체 SHA-256과 일치함을 확인했다.
129개 소스 아카이브, 원래 다운로드 명령 해시 경로 및 별칭을 [목록](ffmpeg-source-cache-inventory.json)에 보존한다.
캐시는 다른 빌드 변형과 소스 전용 빌드 도구도 포함한다. [고지 모음](FFMPEG_DEPENDENCY_NOTICES.md)을 모두 런타임 의존성으로 해석하지 않는다.

## 소스 캐시 조립

소스 키트를 풀고 `join-source-cache.ps1`, `ffmpeg-dependency-sources-manifest.json`과 분할 파일 3개를 같은 폴더에 둔다.
PowerShell에서 `./join-source-cache.ps1`을 실행하면 각 부분과 합친 `download-cache.zip`의 크기 및 SHA-256을 검증한다.
ZIP 안의 `cache.tar.gz`를 꺼내고 Linux/WSL의 GNU tar로 **빌드 레시피 루트**에 푼다.
`tar -xzf /path/to/cache.tar.gz`는 `.cache/downloads`와 원본 심볼릭 링크를 복원한다.

## 재빌드 경로

Linux 또는 WSL 환경, Docker/Buildx, Git 및 upstream 레시피가 요구하는 빌드 자원이 필요하다.
`build-recipes.zip`을 풀고 그 루트에서 캐시를 복원한다. 원본 레시피는 변경하지 않은 ZIP으로 함께 보존한다.
upstream `build.sh`의 `git clone` 다음 `cd ffmpeg` 직후에 아래 행을 추가한 작업 사본을 사용한다.

```bash
git checkout --detach 5c4f5744cd60943125a78db692b728210102f255
```

또는 제공한 `ffmpeg-source.zip`을 FFmpeg 소스로 사용하도록 작업 사본의 clone 단계를 바꿀 수 있다.
레시피의 현재 날짜 기반 `--extra-version`을 `20261022`로 고정하면 원본 날짜 접미사를 보존할 수 있다.
이후 레시피 루트에서 다음을 실행한다.

```bash
./makeimage.sh win64 lgpl 8.1
./build.sh win64 lgpl 8.1
```

패치·의존성 버전·링크 옵션은 레시피의 `scripts.d`, `patches`, `variants`, `addins`에 있고
실제 내장 바이너리의 configure 및 컴파일러 출력은 `FFmpeg-build-info.txt`에 보존한다.
레시피는 컨테이너 기반 이미지와 추가 도구를 받을 때 네트워크를 사용한다.
전체 FFmpeg 재빌드는 이 Windows PC에서 실행하지 않았으며, 현재의 외부 기반 이미지로 비트 단위 동일성이 보장되지는 않는다.

## 수정한 도구 사용

`SOUNDWEIGHTGRAPH_FFMPEG_DIR` 환경 변수에 `ffmpeg.exe`, `ffprobe.exe`가 있는 폴더의 절대 경로를 지정한다.
앱은 명시한 파일을 덮어쓰지 않는다. 파일이 빠졌다면 오류를 안내한다.
환경 변수가 없으면 기본 내장 도구를 추출하고 SHA-256으로 검증한다.

관련 약관 및 Microsoft SDK 확인은 [LICENSING.md](LICENSING.md)에 기록한다.
