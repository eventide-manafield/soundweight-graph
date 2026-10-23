# 검증 기록

검증일: 2026-10-23. Windows 11 x64 build 26200, .NET SDK 10.0.103.
이 기록은 실제 실행 결과와 남은 항목을 구분한다.

## 확인한 결과

- Release 빌드: 경고 0, 오류 0.
- xUnit: 20개 통과, 실패 0, 건너뜀 0.
- 실제 ffprobe/ffmpeg: 6트랙 MP4의 실제 stream index, 무음과 일정한 소리 구분,
  오디오 없는 파일/손상 파일 오류, 한글·공백·`&` 경로.
- 시각 정렬: 시작 PTS가 약 3초인 오디오의 앞 2.5초는 무음이고 3초 지점에 신호가 존재함.
- 가중 평균 예제 78.75, OFF/0가중치 제외, 단일 트랙 100점, 전체 OFF,
  2초 지연 반응 보너스, 일정한 배경음/극저음, 짧은 마지막 프레임, NaN 차단.
- 5시간 분량 특징 배열 6개로 점수 계산. 실제 5시간 합성 MP4 분석 중 취소와 디코더 종료.
- 캐시 roundtrip, JSON 손상/배열 길이 불일치 무효화, 파일 변경으로 키 변경, 파일별 설정 복원.
- 임베드 도구 자동 추출 및 동일 도구 재사용 시 수정 시각 유지.
- 실제 WPF 창: 파일 조사/분석/그래프 표시, 빠른 가중치 변경 시 재디코딩 0회,
  전체 OFF 후 복구, 캐시 재열기 재디코딩 0회, 차트 시간 간격 검증.
- WPF에서 분석 중 취소 및 다른 파일로 전환 후 정상 분석.
- Self-contained single-file publish 성공. 전달 폴더에 `SoundWeightGraph.exe` 1개.
- publish EXE로 위 GUI smoke test 통과.
- .NET Core 런타임 10.0.11을 내장했고 실행 결과에서도 버전 10.0.11 확인. Windows Desktop 런타임 팩은 deps 기준 10.0.3이며, 모든 구성요소의 버전이 같다는 의미는 아니다.
- 한글 경로의 별도 폴더에 EXE만 복사하고 PATH를 System32만 남긴 상태에서도 GUI smoke 통과.
  DOTNET_ROOT는 존재하지 않는 경로로 지정했고 WPF 네이티브 모듈이 번들 추출 폴더에서 로드된 것을 확인.
  이것은 별도의 깨끗한 Windows PC 검증을 대신하지 않는다.
- 코드 서명 상태는 NotSigned.

자동 검사 결과 원본:

- `artifacts/test-results/tests.trx`
- `artifacts/app-smoke.json`, `artifacts/app-smoke.png`
- `artifacts/published-smoke.json`, `artifacts/published-smoke.png`
- `artifacts/standalone-smoke.json`, `artifacts/standalone-environment.json`
- `artifacts/release-manifest.json`: 최종 EXE 크기와 SHA-256

## 해석상의 한계

5시간 파일은 20초짜리 합성 음향을 반복한 회귀 테스트용 파일이다. 실제 OBS의 5시간 방송을 분석한 결과가 아니다.
긴 파일은 취소/파일 전환 검증에 사용했고, 전체 6트랙을 끝까지 디코딩하는 성능 벤치마크는 하지 않았다.
GUI smoke test는 앱의 실제 ViewModel과 실제 창 렌더링을 사용하지만 OS의 마우스/클립보드 입력을 자동 조작하지 않았다.
앱 실행 PC에는 SDK와 런타임이 이미 있으므로, single-file 실행 성공만으로 깨끗한 PC 검증을 대체하지 않는다.
오프라인 설계와 미디어 도구의 네트워크 프로토콜 제한은 구현했지만, PC 네트워크를 실제로 끄고 검사하지는 않았다.

## 후속 호환성 검증 항목

- 런타임 없는 깨끗한 Windows 10/11 환경, 오프라인 실행, 사용자 권한 및 다양한 DPI 확인.
- 실제 녹화본의 후보 점수 품질/정렬과 전체 분석 CPU·메모리·시간 측정.
- 코드 서명 여부 결정 및 SmartScreen UI 검사.
- FFmpeg 전체 재빌드 검증은 수행하지 않음. 원본 소스와 빌드 자료의 수집·해시 검증 결과는 아래 최종 기록 참고.

v0.1.0은 아래 확인된 범위로 제공한다. 위 환경·성능 항목의 검증 완료를 의미하지 않는다.

## 2026-10-23 시간축 조작 및 라이선스 고지 갱신

- Release 재빌드: 경고 0, 오류 0. 기존 xUnit 20개 재실행 통과.
- 0.5초 간격의 36,000개 Signal 점으로 5시간 시간축을 렌더링했다.
- 설치된 ScottPlot 입력 처리기에 휠 위/아래 이벤트를 전달해 가로 확대/축소와 포인터 아래 시각 유지 확인.
- Shift 상태에서 휠 위/아래 이벤트를 전달해 배율을 유지한 좌우 이동 확인.
- 모든 검사에서 세로 점수축 0~100 유지. 원본 0.5초 샘플 간격은 변경하지 않았다.
- 앱 빌드와 단일 EXE 모두 GUI smoke 통과. 물리 마우스 입력과 다양한 DPI는 아직 별도 확인이 필요하다.
- 프로젝트의 Apache-2.0 LICENSE와 NOTICE를 추가하고, 수집한 외부 라이선스 원문과 함께 앱의 고지 리소스에 포함했다.
- 잠금 파일과 실행 의존성의 27개 패키지/런타임 항목을 조사했다. 실제 실행 자산을 가진 23개 항목의 고지를 확보했다.
- 이 단계에서 남아 있던 FFmpeg 소스 수집 및 Windows SDK 매핑은 아래 최종 기록에서 갱신했다. 자세한 근거는 [LICENSING.md](LICENSING.md)에 기록한다.


## 2026-10-23 제품명 변경 검증

- 표시 이름 SoundWeight Graph, 실행 파일/프로젝트/네임스페이스/설정 폴더 SoundWeightGraph로 통일.
- 소스와 문서의 이전 제품명 검색 결과 0건. DLL/EXE 제품명, 파일 설명, 원본 파일명과 회사/저작권 메타데이터 확인.
- 빌드 경고 0, 오류 0. 기존 분석 테스트와 사용자 지정 FFmpeg 경로 검사까지 총 22개 통과.
- 실제 Windows 창의 제목 표시줄과 화면 머리글에서 SoundWeight Graph 표기 확인. 사용자가 실행 후 정상 동작을 확인하고 종료함.
- 새 단일 EXE의 6트랙 분석, 5시간 그래프 탐색, 취소/파일 전환 GUI 검사 통과.
- SDK DLL 두 개를 공식 REDIST 목록과 대조하고 NuGet 원본과 빌드 출력의 SHA-256 일치 확인.

## v0.1.0 배포 자료 최종 확인

- FFmpeg 원본 소스, 고정된 빌드 레시피, 공식 빌드의 의존성 캐시 SHA-256 검증 완료.
- 캐시의 소스 아카이브 129개, 고유 고지 551개 및 각 원문 해시/저장 경로 검증 완료.
- 소스는 별도 릴리스 파일로 제공하고 앱에는 고지 전문만 포함.
- SDK 재배포 약관 적용 범위와 이용 조건을 THIRD_PARTY_TERMS.md 및 릴리스 페이지에 안내.
- 최종 고지를 반영한 EXE의 빌드 및 GUI smoke 결과는 artifacts/published-smoke.json에 기록.

## 트랙 영향력 강조 개발 검증

- 작업 브랜치: `feature/track-influence`. `release/0.1.0`과 `v0.1.0`은 기존 커밋을 유지한다.
- 사용자가 수정한 README 예시를 반영하고 개발 환경·빌드 절차·프로젝트 구조는 `<details>`로 접었다.
- 트랙별 가중 기여도를 동일한 0.5초 간격과 평활화로 계산한다. 공동 반응 보너스는 종합 점수에만 남긴다.
- 체크한 트랙을 고유 색상과 투명도 40%로 표시한다. 표시 토글은 점수·재디코딩·확대 상태를 바꾸지 않는다.
- 빌드 경고 0, 오류 0. xUnit 24개 통과. 가중치 비율, 짧은 트랙의 뒤쪽 무음 처리, 평활화, 제외 트랙을 검사했다.
- 실제 WPF와 테스트용 단일 EXE에서 복수 트랙 강조·해제, 사용 해제·가중치 0, 원래 설정 복원, 점수 불변 및 재디코딩 0회를 확인했다.
- 5시간 Signal 데이터에 강조 선 2개를 추가한 상태에서 휠 확대·축소와 Shift+휠 이동을 확인했다.
- 체크박스는 ViewModel 경로로 검사했고 렌더링 이미지를 확인했다. 물리 마우스로 체크박스를 누르는 OS 입력 자동 검사는 수행하지 않았다.
- 원본 결과: `artifacts/test-results/track-influence.trx`, `artifacts/track-influence-published-smoke.json` 및 같은 이름의 PNG.
- 테스트용 실행 파일: `artifacts/preview/SoundWeightGraph.exe`. 공개된 v0.1.0 실행 파일과 릴리스는 변경하지 않았다.

## 상단 화면 정리

- 본문 좌상단의 프로그램명·큰 안내 제목·부제목을 제거하고 툴바와 그래프를 위로 당겼다.
- 오픈소스 고지는 툴바 오른쪽에 배치했다. 그래프 아래 마지막 후보 점수 설명 문장을 제거했다.
- 취소 버튼은 `작업 취소`로 표시하고 Busy 상태에서만 보이게 했다. 실제 분석 취소 동작은 유지한다.
- 테스트용 EXE 빌드와 기존 GUI 검사 통과. 렌더링 이미지에서 제목 영역 제거, 넓어진 그래프 및 대기 상태의 취소 버튼 숨김을 확인했다.
- 최신 확인용 EXE: `artifacts/ui-preview/SoundWeightGraph.exe`. 결과: `artifacts/compact-ui-smoke.json`, `artifacts/compact-ui-smoke.png`.

## 분석 중 진행 표시

- 하단 상시 상태 문구를 제거했다. 진행 막대와 여백은 분석 중에만 나타나고 완료·취소·실패 시 접힌다.
- 파일 조사나 가중치 재계산에는 진행 막대를 표시하지 않는다. 비어 있지 않은 오류/경고 안내는 별도로 유지한다.
- 최신 테스트용 EXE: `artifacts/progress-preview/SoundWeightGraph.exe`.
- 단일 EXE GUI 검사에서 분석 중 Visible, 완료 및 취소 후 Collapsed 상태를 확인했다. 결과: `artifacts/analysis-progress-smoke.json` 및 PNG.

## v0.2.0 최종 검증

- 오디오 트랙 제목 아래 부제목까지 제거했다. README 미리보기는 최종 배포 EXE의 실제 렌더링이다.
- 버전 0.2.0 / 파일 버전 0.2.0.0 확인. Release 빌드 경고 0, 오류 0. xUnit 24개 재실행 통과.
- 배포 EXE의 트랙 강조, 점수 불변, 5시간 그래프 확대·이동, 분석 취소·파일 전환, 완료·취소 후 진행 막대 숨김 검사 통과.
- 종속 라이브러리와 내장 FFmpeg 버전은 v0.1.0과 동일하며 27개 원문 고지 해시를 재검증했다.
- 원본 결과: `artifacts/test-results/release-0.2.0.trx`, `artifacts/release-0.2.0-smoke.json`, `artifacts/release-0.2.0-smoke.png`.
- 최종 EXE: `artifacts/release-0.2.0/SoundWeightGraph.exe`. 해시/크기는 `artifacts/release-manifest.json` 및 릴리스 `SHA256SUMS.txt`에 기록한다.

## v0.2.0 제목 패치

- 그래프 제목을 `소리 밀도 점수`로 변경하고 바로 아래 부제목을 제거했다.
- 점수 계산 변경 없이 v0.2.0의 실행 파일과 체크섬을 교체한다. README 미리보기도 새 화면으로 갱신했다.
- 빌드 경고 0, 오류 0 및 기존 GUI 검사 통과. 결과: `artifacts/score-title-smoke.json`.
