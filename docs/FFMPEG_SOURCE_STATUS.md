# FFmpeg 소스 보관 기록

SoundWeight Graph의 내장 FFmpeg/ffprobe는 BtbN `n8.1.3-23-g5c4f5744cd-20261022`의 수정하지 않은 실행 파일이다.
앱과는 별도 프로세스로 통신한다. 앱 코드는 Apache-2.0, 이 도구는 LGPL-3.0-or-later 조건을 유지한다.

## 확보한 자료

| 자료 | 식별자 | SHA-256 |
|---|---|---|
| FFmpeg 소스 | `5c4f5744cd60943125a78db692b728210102f255` | `C6978FA52D470FF3E3E5E96B54F51155580B61FFFC32447304189719B281B522` |
| BtbN 빌드 레시피 | `937868360a21d711ec94a33f5591d345ad9b7754` | `3D782F1B71B4F6CEF89C9E0014176542D071B1DFBF5DE41B9F96AAC6A442B1AF` |

원본 아카이브는 `third_party/sources/ffmpeg-source.zip`, `third_party/sources/build-recipes.zip`에 보관한다.
소스 수집만으로 재빌드 검증까지 완료한 것은 아니다.

## 의존성 소스 캐시

- 공식 실행 기록: <https://github.com/BtbN/FFmpeg-Builds/actions/runs/39776938430>
- artifact 이름 / ID: `download-cache` / `12424698518`
- 크기: 2,210,375,345 bytes
- 공식 SHA-256: `F89C6C43969D92C8B95CB459A90FBAFBC743D3D3CC4D16C610317F9CEEF922F8`
- 보존 상태와 빌드 커밋: `third_party/sources/source-cache-metadata.json`

이 아카이브에는 다른 빌드 변형용 자료도 포함될 수 있다. 확보 후 실제 사용한 구성요소와 고지를 대조해야 한다.
캐시 파일은 최종 SHA-256 검증을 통과한 경우에만 `third_party/sources/download-cache.zip`으로 보관한다.
GitHub Actions artifact는 영구 배포 주소가 아니므로 공개 EXE 배포 시 보관한 소스를 함께 제공해야 한다.

## 수정한 도구 사용

`SOUNDWEIGHTGRAPH_FFMPEG_DIR` 환경 변수에 `ffmpeg.exe`, `ffprobe.exe`가 있는 폴더의 절대 경로를 지정한다.
앱은 명시적으로 지정한 파일을 덮어쓰지 않는다. 파일이 빠졌다면 기본 도구로 조용히 대체하지 않고 오류를 안내한다.
환경 변수를 지정하지 않으면 기본 내장 도구를 추출하고 SHA-256으로 검증한다.

## 공개 배포 전에 마칠 항목

1. 의존성 캐시의 체크섬 및 실제 포함 라이브러리와 고지를 대조한다.
2. 소스, 빌드 레시피, 사용한 의존성 소스와 필요한 재링크 자료를 함께 배포할 형태로 묶는다.
3. EXE 다운로드 페이지에서 그 소스를 실제로 받을 수 있게 제공한다.

관련 약관 및 Microsoft SDK 확인은 [LICENSING.md](LICENSING.md)에 기록한다.
