# Changelog

모든 주요 변경 사항은 이 파일에 기록됩니다.

이 프로젝트는 [시맨틱 버저닝(Semantic Versioning)](https://semver.org/spec/v2.0.0.html)을 준수합니다.

## [1.0.0] - 2025-09-22

### Added

-   **Gemini Image Generator** 에디터 윈도우 초기 버전 출시.
-   Google Gemini API를 이용한 텍스트-이미지(Text-to-Image) 생성 기능 구현.
-   기존 이미지와 프롬프트를 이용한 이미지 편집(Image-to-Image) 기능 구현.
-   API Key, 모델명, 출력 폴더 설정 및 `EditorPrefs`를 통한 저장 기능 추가.
-   자주 사용하는 프롬프트를 저장, 로드, 삭제할 수 있는 프리셋 시스템 구현.
-   1:1, 16:9 등 다양한 해상도 및 화면 비율 프리셋 기능 추가.
-   API에서 지원하지 않는 해상도를 위해 다운로드 후 자동 리사이징하는 옵션 추가.
-   생성된 이미지를 미리 볼 수 있는 Preview 영역 및 상태 메시지 창 추가.
-   `async/await` 및 `UnityWebRequest`를 이용한 비동기 네트워크 요청 및 재시도 로직 구현.