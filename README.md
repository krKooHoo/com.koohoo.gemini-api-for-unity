# Gemini API for Unity

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
![Unity Version](https://img.shields.io/badge/Unity-2022.3%2B-blue.svg)

Unity 에디터 내에서 Google Gemini API를 사용하여 이미지를 생성하고 편집할 수 있는 에디터 툴입니다.

## 개요

이 패키지는 Unity 개발자가 외부 툴을 사용하지 않고 에디터 환경에서 직접 텍스트 프롬프트나 기존 이미지를 기반으로 새로운 그래픽 에셋을 생성할 수 있도록 돕습니다. 게임 프로토타이핑, 컨셉 아트, 아이콘 제작 등 다양한 과정에서 작업 효율을 높일 수 있습니다.

## 주요 기능

-   **텍스트-이미지 생성**: 텍스트 프롬프트를 입력하여 이미지를 생성합니다.
-   **이미지 편집**: 기존 이미지와 텍스트 프롬프트를 함께 사용하여 이미지를 수정하거나 변형합니다.
-   **프롬프트 프리셋 관리**: 자주 사용하는 프롬프트를 저장하고 불러올 수 있습니다.
-   **해상도 및 비율 제어**: 1:1, 16:9 등 일반적인 비율 프리셋과 사용자 지정 해상도를 지원합니다.
-   **자동 리사이징**: API가 특정 해상도를 지원하지 않는 경우, 이미지 다운로드 후 목표 해상도로 자동 리사이징하는 옵션을 제공합니다.
-   **간편한 설정**: API 키, 모델명, 결과물 저장 경로를 에디터 내에서 쉽게 설정하고 저장할 수 있습니다.

## 요구 사항

-   Unity 2022.3 LTS 버전 이상
-   [Google AI Studio](https://aistudio.google.com/app/apikey)에서 발급받은 Gemini API Key
-   `com.unity.nuget.newtonsoft-json` 패키지 (UPM을 통해 설치 시 자동 설치됩니다)

## 설치 방법

Unity Package Manager(UPM)를 통해 Git URL로 설치할 수 있습니다.

1.  Unity 에디터에서 **Window > Package Manager**를 엽니다.
2.  왼쪽 상단의 **'+'** 아이콘을 클릭하고 **'Add package from git URL...'**을 선택합니다.
3.  아래의 URL을 입력하고 'Add' 버튼을 누릅니다.

    ```
    https://github.com/krKooHoo/com.koohoo.gemini-api-for-unity.git
    ```

4.  설치가 완료되면 메뉴에 `Tools/Gemini Image Generator`가 추가됩니다.

## 사용 방법

1.  Unity 상단 메뉴에서 **Tools > Gemini Image Generator**를 클릭하여 창을 엽니다.
2.  **API Key** 입력 필드에 발급받은 Gemini API 키를 붙여넣고 **'Save'** 버튼을 누릅니다.
3.  **Prompt** 영역에 원하는 이미지에 대한 설명을 작성합니다.
4.  (선택 사항) 이미지를 편집하려면 **'Image To Edit'** 필드에 프로젝트 내의 이미지를 드래그 앤 드롭합니다.
5.  (선택 사항) **Resolution / Aspect Presets** 에서 원하는 이미지 크기와 비율을 설정합니다.
6.  **'Generate Image'** 또는 **'Edit Image'** 버튼을 클릭하여 이미지 생성을 요청합니다.
7.  요청이 완료되면 **Preview** 영역에 결과 이미지가 표시되고, 설정된 **Output Folder**에 자동으로 저장됩니다.

## 라이선스

이 프로젝트는 [MIT 라이선스](LICENSE.md)를 따릅니다.
