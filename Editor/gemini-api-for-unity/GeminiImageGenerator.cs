// File: Assets/Editor/GeminiImageGeneratorWindow.cs
using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public class GeminiImageGeneratorWindow : EditorWindow
{
    // ====== EditorPrefs Keys ======
    private const string EDITORPREF_API_KEY   = "Gemini_Api_Key_V1";
    private const string EDITORPREF_MODEL     = "Gemini_Model_V1";
    private const string EDITORPREF_OUTDIR    = "Gemini_OutDir_V1";
    private const string EDITORPREF_PROMPTS   = "Gemini_Prompt_Presets_V1";

    // ====== Defaults ======
    private const string DEFAULT_OUTPUT_FOLDER = "Assets/GeneratedImages";
    private const int    DEFAULT_TIMEOUT_SEC   = 180;

    // ====== UI State ======
    private string apiKey = "";
    private string model = "gemini-2.5-flash-image-preview";
    private string prompt = "A photorealistic shot of a nano banana dish in a fancy restaurant";
    private Texture2D imageToEdit = null;
    private string outputFolder = DEFAULT_OUTPUT_FOLDER;
    private bool isRequestRunning = false;
    private string statusMessage = "";

    // Preview
    private Texture2D previewTexture = null;
    private string lastSavedPath = "";

    // ====== Resolution / Aspect Presets ======
    private enum AspectPreset
    {
        _1x1,
        _16x9,
        _4x3,
        _9x16,
        Custom
    }
    private AspectPreset aspectPreset = AspectPreset._1x1;
    private int sizePreset = 1024; // base size for shorter edge
    private int customWidth = 1024;
    private int customHeight = 1024;
    private bool allowResizeAfterDownload = true; // API 사이즈 미지원 대비, 다운로드 후 리사이즈

    // ====== Prompt Presets ======
    [Serializable]
    private class PromptPreset
    {
        public string Name;
        public string Text;
    }
    private List<PromptPreset> promptPresets = new List<PromptPreset>();
    private int selectedPromptPresetIndex = -1;
    private string newPresetName = "New Preset";

    [MenuItem("Tools/Gemini Image Generator")]
    public static void ShowWindow()
    {
        var window = GetWindow<GeminiImageGeneratorWindow>("Gemini Image Gen");
        window.minSize = new Vector2(760, 520);
    }

    private void OnEnable()
    {
        apiKey = EditorPrefs.GetString(EDITORPREF_API_KEY, "");
        model = EditorPrefs.GetString(EDITORPREF_MODEL, model);
        outputFolder = EditorPrefs.GetString(EDITORPREF_OUTDIR, outputFolder);
        LoadPromptPresetsFromPrefs();
    }

    private void OnGUI()
    {
        DrawHeader();
        EditorGUILayout.Space(4);
        DrawPromptArea();
        EditorGUILayout.Space(4);
        DrawImageEditAndPresetsArea();
        EditorGUILayout.Space(4);
        DrawGenerateButtons();
        EditorGUILayout.Space(8);
        DrawPreviewArea();
        EditorGUILayout.Space(8);
        DrawStatusArea();
    }

    private void DrawHeader()
    {
        GUILayout.Label("Gemini Image Generator (Editor)", EditorStyles.boldLabel);

        EditorGUILayout.BeginVertical("box");
        // API Key
        EditorGUILayout.LabelField("API Key (x-goog-api-key)", EditorStyles.label);
        EditorGUILayout.BeginHorizontal();
        apiKey = EditorGUILayout.TextField(apiKey);
        if (GUILayout.Button("Save", GUILayout.Width(60)))
        {
            EditorPrefs.SetString(EDITORPREF_API_KEY, apiKey);
            SetStatus("API Key 저장됨 (EditorPrefs).");
        }
        if (GUILayout.Button("Clear", GUILayout.Width(60)))
        {
            apiKey = "";
            EditorPrefs.DeleteKey(EDITORPREF_API_KEY);
            SetStatus("API Key 삭제됨.");
        }
        EditorGUILayout.EndHorizontal();

        // Model
        EditorGUILayout.BeginHorizontal();
        model = EditorGUILayout.TextField("Model", model);
        if (GUILayout.Button("Save Model", GUILayout.Width(100)))
        {
            EditorPrefs.SetString(EDITORPREF_MODEL, model);
            SetStatus("Model 저장됨.");
        }
        EditorGUILayout.EndHorizontal();

        // Output Folder
        EditorGUILayout.BeginHorizontal();
        outputFolder = EditorGUILayout.TextField("Output Folder", outputFolder);
        if (GUILayout.Button("Save Output Folder", GUILayout.Width(160)))
        {
            EditorPrefs.SetString(EDITORPREF_OUTDIR, outputFolder);
            SetStatus("Output Folder 저장됨.");
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
    }

    private void DrawPromptArea()
    {
        EditorGUILayout.LabelField("Prompt (텍스트)", EditorStyles.label);
        prompt = EditorGUILayout.TextArea(prompt, GUILayout.Height(80));

        // Prompt Presets
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("Prompt Presets", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        string[] names = promptPresets.Select(p => p.Name).ToArray();
        int newIndex = EditorGUILayout.Popup("Select Preset", selectedPromptPresetIndex, names);
        if (newIndex != selectedPromptPresetIndex)
        {
            selectedPromptPresetIndex = newIndex;
        }
        if (GUILayout.Button("Load", GUILayout.Width(80)))
        {
            if (selectedPromptPresetIndex >= 0 && selectedPromptPresetIndex < promptPresets.Count)
            {
                prompt = promptPresets[selectedPromptPresetIndex].Text;
                SetStatus($"프리셋 로드: {promptPresets[selectedPromptPresetIndex].Name}");
            }
        }
        if (GUILayout.Button("Delete", GUILayout.Width(80)))
        {
            if (selectedPromptPresetIndex >= 0 && selectedPromptPresetIndex < promptPresets.Count)
            {
                string delName = promptPresets[selectedPromptPresetIndex].Name;
                promptPresets.RemoveAt(selectedPromptPresetIndex);
                selectedPromptPresetIndex = -1;
                SavePromptPresetsToPrefs();
                SetStatus($"프리셋 삭제: {delName}");
            }
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(2);
        EditorGUILayout.BeginHorizontal();
        newPresetName = EditorGUILayout.TextField("New Preset Name", newPresetName);
        if (GUILayout.Button("Save Current as New", GUILayout.Width(180)))
        {
            if (string.IsNullOrWhiteSpace(newPresetName) == true)
            {
                SetStatus("프리셋 이름을 입력하세요.");
            }
            else
            {
                // 같은 이름 있으면 덮어쓰기
                var exist = promptPresets.FirstOrDefault(p => p.Name == newPresetName);
                if (exist != null)
                {
                    exist.Text = prompt;
                }
                else
                {
                    promptPresets.Add(new PromptPreset { Name = newPresetName, Text = prompt });
                }
                SavePromptPresetsToPrefs();
                SetStatus($"프리셋 저장: {newPresetName}");
            }
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
    }

    private void DrawImageEditAndPresetsArea()
    {
        EditorGUILayout.BeginVertical("box");
        // Image To Edit
        imageToEdit = (Texture2D)EditorGUILayout.ObjectField("Image To Edit (선택)", imageToEdit, typeof(Texture2D), false);

        // Aspect / Resolution
        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Resolution / Aspect Presets", EditorStyles.boldLabel);
        aspectPreset = (AspectPreset)EditorGUILayout.EnumPopup("Aspect", aspectPreset);
        sizePreset = EditorGUILayout.IntPopup("Base Size (shorter edge)", sizePreset,
            new[] { "512", "1024", "1536", "2048" }, new[] { 512, 1024, 1536, 2048 });

        if (aspectPreset == AspectPreset.Custom)
        {
            EditorGUILayout.BeginHorizontal();
            customWidth = EditorGUILayout.IntField("Custom Width", customWidth);
            customHeight = EditorGUILayout.IntField("Custom Height", customHeight);
            EditorGUILayout.EndHorizontal();
        }

        allowResizeAfterDownload = EditorGUILayout.ToggleLeft("Resize After Download (API 사이즈 미지원 대비)", allowResizeAfterDownload == true);

        EditorGUILayout.HelpBox(
            "일부 모델/엔드포인트는 해상도/비율 파라미터를 직접 받지 않습니다. 위 프리셋은 '다운로드 후 리사이즈'로 보정합니다.",
            MessageType.None);
        EditorGUILayout.EndVertical();
    }

    private void DrawGenerateButtons()
    {
        EditorGUILayout.BeginHorizontal();
        GUI.enabled = isRequestRunning == false;
        if (GUILayout.Button("Generate Image (Text → Image)", GUILayout.Height(36)))
        {
            _ = GenerateFromPromptAsync();
        }
        if (GUILayout.Button("Edit Image (Image + Prompt)", GUILayout.Height(36)))
        {
            if (imageToEdit == null)
            {
                SetStatus("이미지를 선택하세요 (Image To Edit).");
            }
            else
            {
                _ = EditImageAsync();
            }
        }
        GUI.enabled = true;
        EditorGUILayout.EndHorizontal();

        if (isRequestRunning == true)
        {
            EditorGUILayout.HelpBox("요청 처리 중입니다. 잠시만 기다려주세요.", MessageType.None);
        }
    }

    private void DrawPreviewArea()
    {
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);

        if (previewTexture != null)
        {
            float maxW = EditorGUIUtility.currentViewWidth - 60f;
            float ratio = (float)previewTexture.height / (float)previewTexture.width;
            float drawW = Mathf.Min(maxW, 512f);
            float drawH = drawW * ratio;

            Rect r = GUILayoutUtility.GetRect(drawW, drawH, GUILayout.ExpandWidth(false));
            EditorGUI.DrawPreviewTexture(r, previewTexture, null, ScaleMode.ScaleToFit);
            EditorGUILayout.LabelField($"Last Saved: {lastSavedPath}");
        }
        else
        {
            EditorGUILayout.HelpBox("생성된 이미지가 여기 미리보기로 표시됩니다.", MessageType.Info);
        }
        EditorGUILayout.EndVertical();
    }

    private void DrawStatusArea()
    {
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("Status:", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(statusMessage, MessageType.Info);
        EditorGUILayout.EndVertical();
    }

    private void SetStatus(string message)
    {
        statusMessage = message;
        Repaint();
        Debug.Log("[GeminiImageGen] " + message);
    }

    // ========================= Requests =========================

    private async Task GenerateFromPromptAsync()
    {
        if (string.IsNullOrEmpty(apiKey) == true)
        {
            SetStatus("API Key를 입력하고 저장하세요.");
            return;
        }

        isRequestRunning = true;
        SetStatus("텍스트 → 이미지 요청 준비중...");
        EditorUtility.DisplayProgressBar("Gemini", "Requesting...", 0.1f);

        try
        {
            var payloadObj = new
            {
                contents = new object[]
                {
                    new {
                        parts = new object[] {
                            new { text = prompt }
                        }
                    }
                }
            };

            string url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent";
            string json = JsonConvert.SerializeObject(payloadObj);

            var uwr = await SendWithRetryAsync(url, json, apiKey, 2);
            EditorUtility.DisplayProgressBar("Gemini", "Parsing response...", 0.9f);

            if (uwr == null)
            {
                SetStatus("요청 중 예외가 발생했습니다.");
                Debug.LogError("[GeminiImageGen] UnityWebRequest null");
                return;
            }

            if (uwr.result != UnityWebRequest.Result.Success)
            {
                string errText = uwr.downloadHandler != null ? uwr.downloadHandler.text : "<no response body>";
                SetStatus($"요청 실패: {uwr.error}");
                Debug.LogError($"[GeminiImageGen] Error: {uwr.error}\nResponse: {errText}");
                EditorUtility.DisplayDialog("Gemini Error", $"Request failed: {uwr.error}\n\nResponse:\n{errText}", "OK");
            }
            else
            {
                string responseText = uwr.downloadHandler.text;
                SaveGeneratedImagesFromResponse(responseText);
            }
        }
        catch (Exception ex)
        {
            SetStatus("오류: " + ex.Message);
            Debug.LogException(ex);
        }
        finally
        {
            isRequestRunning = false;
            EditorUtility.ClearProgressBar();
        }
    }

    private async Task EditImageAsync()
    {
        if (string.IsNullOrEmpty(apiKey) == true)
        {
            SetStatus("API Key를 입력하고 저장하세요.");
            return;
        }

        isRequestRunning = true;
        
        
        
        SetStatus("이미지 편집 요청 준비중...");
        EditorUtility.DisplayProgressBar("Gemini", "Preparing image...", 0.1f);

        try
        {
            //byte[] pngBytes = imageToEdit.EncodeToPNG();
            byte[] pngBytes = EncodeTextureSafeToPNG(imageToEdit);
            if (pngBytes == null || pngBytes.Length == 0)
            {
                SetStatus("이미지 PNG 인코딩에 실패했습니다. 콘솔 로그를 확인하세요.");
                return;
            }
            
            string base64Image = Convert.ToBase64String(pngBytes);

            var payloadObj = new
            {
                contents = new object[]
                {
                    new {
                        parts = new object[] {
                            new { text = prompt },
                            new {
                                inline_data = new {
                                    mime_type = "image/png",
                                    data = base64Image
                                }
                            }
                        }
                    }
                }
            };

            string url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent";
            string json = JsonConvert.SerializeObject(payloadObj);

            var uwr = await SendWithRetryAsync(url, json, apiKey, 2);
            EditorUtility.DisplayProgressBar("Gemini", "Parsing response...", 0.9f);

            if (uwr == null)
            {
                SetStatus("요청 중 예외가 발생했습니다.");
                return;
            }

            if (uwr.result != UnityWebRequest.Result.Success)
            {
                string errText = uwr.downloadHandler != null ? uwr.downloadHandler.text : "<no response body>";
                SetStatus($"요청 실패: {uwr.error}");
                Debug.LogError($"[GeminiImageGen] Error: {uwr.error}\nResponse: {errText}");
                EditorUtility.DisplayDialog("Gemini Error", $"Request failed: {uwr.error}\n\nResponse:\n{errText}", "OK");
            }
            else
            {
                string responseText = uwr.downloadHandler.text;
                SaveGeneratedImagesFromResponse(responseText);
            }
        }
        catch (Exception ex)
        {
            SetStatus("오류: " + ex.Message);
            Debug.LogException(ex);
        }
        finally
        {
            isRequestRunning = false;
            EditorUtility.ClearProgressBar();
        }
    }

    // 재시도 + 타임아웃
    private async Task<UnityWebRequest> SendWithRetryAsync(string url, string jsonBody, string apiKeyHeader, int maxRetry = 2)
    {
        int attempt = 0;
        while (attempt <= maxRetry)
        {
            var uwr = await SendPostRequestAsync(url, jsonBody, apiKeyHeader, DEFAULT_TIMEOUT_SEC);
            bool shouldRetry = false;

            if (uwr == null) shouldRetry = true;
            else
            {
                int code = (int)uwr.responseCode;
                if (uwr.result != UnityWebRequest.Result.Success && (code == 429 || (code >= 500 && code < 600)))
                {
                    shouldRetry = true;
                }
            }

            if (shouldRetry == false) return uwr;

            attempt++;
            if (attempt <= maxRetry)
            {
                int backoffMs = 800 * attempt;
                await Task.Delay(backoffMs);
            }
            else return uwr;
        }
        return null;
    }

    private Task<UnityWebRequest> SendPostRequestAsync(string url, string jsonBody, string apiKeyHeader, int timeoutSec)
    {
        var tcs = new TaskCompletionSource<UnityWebRequest>();
        try
        {
            var request = new UnityWebRequest(url, "POST");
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("x-goog-api-key", apiKeyHeader);
            request.timeout = timeoutSec;

            var op = request.SendWebRequest();
            op.completed += _ => tcs.SetResult(request);
        }
        catch (Exception ex)
        {
            tcs.SetException(ex);
        }
        return tcs.Task;
    }

    // ====================== Response & Preview ======================

    // JSON 전체에서 base64-like 문자열을 찾아 이미지로 저장
    private void SaveGeneratedImagesFromResponse(string jsonResponse)
    {
        if (string.IsNullOrWhiteSpace(jsonResponse) == true)
        {
            SetStatus("응답이 비어있습니다.");
            Debug.LogWarning("[GeminiImageGen] Empty response.");
            return;
        }

        // 안전한 디렉토리 생성
        if (string.IsNullOrEmpty(outputFolder) == true)
            outputFolder = DEFAULT_OUTPUT_FOLDER;
        if (!Directory.Exists(outputFolder))
            Directory.CreateDirectory(outputFolder);

        try
        {
            var foundBase64 = ExtractBase64StringsFromJson(jsonResponse);
            int savedCount = 0;
            Texture2D firstTex = null;
            string firstPath = "";

            for (int i = 0; i < foundBase64.Count; i++)
            {
                string raw = foundBase64[i];

                // data URL 처리
                string pureB64 = raw;
                string mimeFromDataUrl = null;
                if (raw.StartsWith("data:", StringComparison.OrdinalIgnoreCase) == true)
                {
                    int semi = raw.IndexOf(';');
                    int comma = raw.IndexOf(',');
                    if (semi > 5 && comma > semi)
                    {
                        mimeFromDataUrl = raw.Substring(5, semi - 5); // e.g., image/png
                        pureB64 = raw.Substring(comma + 1);
                    }
                }

                byte[] bytes;
                try
                {
                    bytes = Convert.FromBase64String(pureB64);
                }
                catch
                {
                    // base64가 아니면 스킵
                    continue;
                }

                // mime 추정 -> 확장자
                string fileExt = ".png";
                string mimeFromJson = TryFindNearestMimeTypeForData(jsonResponse, raw);
                string mime = mimeFromDataUrl ?? mimeFromJson;
                if (string.IsNullOrEmpty(mime) == false)
                {
                    if (mime.Contains("jpeg") || mime.Contains("jpg")) fileExt = ".jpg";
                    else if (mime.Contains("png")) fileExt = ".png";
                    else if (mime.Contains("webp")) fileExt = ".webp";
                }

                // 파일 저장 경로
                string fileName = $"gemini_gen_{DateTime.Now.ToString("yyyyMMdd_HHmmss")}_{i}{fileExt}";
                string filePath = Path.Combine(outputFolder, fileName);
                File.WriteAllBytes(filePath, bytes);

                // Import
                AssetDatabase.ImportAsset(filePath);

                // 리사이즈 옵션
                if (allowResizeAfterDownload == true)
                {
                    TryResizeImportedTexture(filePath);
                }

                // 첫 이미지 미리보기 로드
                if (savedCount == 0)
                {
                    firstPath = filePath;
                    firstTex = AssetDatabase.LoadAssetAtPath<Texture2D>(filePath);
                }

                savedCount++;
            }

            // Preview 표시
            if (firstTex != null)
            {
                previewTexture = firstTex;
                lastSavedPath = firstPath;
            }

            // 진단 필드 요약 (가능한 경우)
            TryAppendDiagnosticsFromResponse(jsonResponse);

            if (savedCount == 0)
            {
                SetStatus("응답에서 이미지 데이터를 찾지 못했습니다. 콘솔 로그와 원문 확인 필요.");
                Debug.LogWarning("[GeminiImageGen] No base64 images found in response. Response:\n" + jsonResponse);
                EditorUtility.DisplayDialog("No Image Found", "응답에서 이미지 데이터를 찾지 못했습니다. 콘솔에 원문이 출력됩니다.", "OK");
            }
            else
            {
                SetStatus($"이미지 저장 완료: {savedCount}개. 경로: {outputFolder}");
            }
        }
        catch (Exception ex)
        {
            SetStatus("응답 파싱 오류: " + ex.Message);
            Debug.LogException(ex);
            Debug.LogError("[GeminiImageGen] Response:\n" + jsonResponse);
        }
    }

    // 리사이즈: 현재 프리셋으로 결정된 목표 사이즈로 Texture 리샘플
    private void TryResizeImportedTexture(string assetPath)
    {
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        if (tex == null) return;

        int targetW, targetH;
        GetTargetResolution(tex.width, tex.height, out targetW, out targetH);

        // 동일 크기면 패스
        if (targetW == tex.width && targetH == tex.height) return;

        var resized = ResizeTextureWithRT(tex, targetW, targetH);
        if (resized == null) return;

        // PNG로 다시 저장
        byte[] outPng = resized.EncodeToPNG();
        if (outPng != null)
        {
            File.WriteAllBytes(assetPath, outPng);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
        }

        // 프리뷰 최신화
        if (previewTexture == tex) previewTexture = resized;
    }

    // 목표 해상도 계산 (aspectPreset/sizePreset/custom)
    private void GetTargetResolution(int srcW, int srcH, out int targetW, out int targetH)
    {
        if (aspectPreset == AspectPreset.Custom)
        {
            targetW = Mathf.Max(8, customWidth);
            targetH = Mathf.Max(8, customHeight);
            return;
        }

        // 단축 변(짧은 변)을 sizePreset으로 맞추고, 비율에 맞게 긴 변 계산
        float targetAspect = 1f;
        switch (aspectPreset)
        {
            case AspectPreset._1x1: targetAspect = 1f; break;
            case AspectPreset._16x9: targetAspect = 16f / 9f; break;
            case AspectPreset._4x3: targetAspect = 4f / 3f; break;
            case AspectPreset._9x16: targetAspect = 9f / 16f; break;
        }

        // 짧은 변을 sizePreset
        if (targetAspect >= 1f)
        {
            // 가로가 더 김 → 세로가 짧은 변
            targetH = sizePreset;
            targetW = Mathf.RoundToInt(targetH * targetAspect);
        }
        else
        {
            // 세로가 더 김 → 가로가 짧은 변
            targetW = sizePreset;
            targetH = Mathf.RoundToInt(targetW / targetAspect);
        }
    }

    // 고품질 리사이즈 (RT + Blit)
    private Texture2D ResizeTextureWithRT(Texture2D src, int width, int height)
    {
        var prevRT = RenderTexture.active;
        var rt = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
        try
        {
            Graphics.Blit(src, rt);
            RenderTexture.active = rt;
            var dst = new Texture2D(width, height, TextureFormat.RGBA32, false);
            dst.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            dst.Apply(false, false);
            return dst;
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[GeminiImageGen] Resize 실패: " + ex.Message);
            return null;
        }
        finally
        {
            RenderTexture.active = prevRT;
            RenderTexture.ReleaseTemporary(rt);
        }
    }

    private void TryAppendDiagnosticsFromResponse(string jsonResponse)
    {
        try
        {
            var root = JToken.Parse(jsonResponse);
            var finish = root.SelectTokens("..finishReason").FirstOrDefault();
            var safeties = root.SelectTokens("..safetyRatings[*].category").Select(t => t.Value<string>()).ToList();

            if (finish != null || safeties.Count > 0)
            {
                var msg = $"finishReason={finish?.ToString() ?? "n/a"}";
                if (safeties.Count > 0) msg += $" | safety={string.Join(",", safeties)}";
                SetStatus(statusMessage + " | " + msg);
            }
        }
        catch { /* optional */ }
    }

    // JSON 내 문자열 토큰 중 'base64 같음' 규칙으로 추출
    private List<string> ExtractBase64StringsFromJson(string json)
    {
        var results = new List<string>();

        try
        {
            var token = JToken.Parse(json);

            // 1) 먼저 일반적으로 알려진 경로 시도
            var specificTokens = token.SelectTokens("..inline_data.data");
            foreach (var t in specificTokens)
            {
                if (t.Type == JTokenType.String)
                {
                    string s = t.Value<string>();
                    if (IsLikelyBase64Image(s) == true) results.Add(s);
                }
            }

            var altTokens = token.SelectTokens("..inlineData.data");
            foreach (var t in altTokens)
            {
                if (t.Type == JTokenType.String)
                {
                    string s = t.Value<string>();
                    if (IsLikelyBase64Image(s) == true) results.Add(s);
                }
            }

            // 2) 모든 문자열 토큰을 스캔(중복 제거)
            foreach (var leaf in token.SelectTokens("..*").Where(t => t.Type == JTokenType.String))
            {
                string s = leaf.Value<string>();
                if (string.IsNullOrEmpty(s) == false && IsLikelyBase64Image(s) == true && results.Contains(s) == false)
                {
                    results.Add(s);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[GeminiImageGen] ExtractBase64StringsFromJson 예외: " + ex.Message);
        }

        return results;
    }

    // base64 체크: 길이/문자 패턴(완화)
    private bool IsLikelyBase64Image(string s)
    {
        if (string.IsNullOrEmpty(s) == true) return false;
        if (s.StartsWith("data:", StringComparison.OrdinalIgnoreCase) == true) return true; // data URL은 바로 인정
        if (s.Length < 300) return false; // 경험적 하한
        if (Regex.IsMatch(s, @"^[A-Za-z0-9+/=\r\n]+$") == false) return false;
        return true;
    }

    private string TryFindNearestMimeTypeForData(string json, string b64Value)
    {
        try
        {
            var root = JToken.Parse(json);
            foreach (var part in root.SelectTokens("..parts[*]"))
            {
                var inline = part["inline_data"] ?? part["inlineData"];
                if (inline != null)
                {
                    var dataToken = inline["data"];
                    if (dataToken != null && dataToken.Type == JTokenType.String && dataToken.Value<string>() == b64Value)
                    {
                        var mt = inline["mime_type"] ?? inline["mimeType"];
                        if (mt != null && mt.Type == JTokenType.String) return mt.Value<string>();
                    }
                }
            }
        }
        catch { /* noop */ }
        return null;
    }

    // ====================== Prompt Presets ======================

    private void LoadPromptPresetsFromPrefs()
    {
        promptPresets.Clear();
        try
        {
            string json = EditorPrefs.GetString(EDITORPREF_PROMPTS, "");
            if (string.IsNullOrEmpty(json) == false)
            {
                var list = JsonConvert.DeserializeObject<List<PromptPreset>>(json);
                if (list != null) promptPresets = list;
            }
        }
        catch { /* ignore */ }
    }

    private void SavePromptPresetsToPrefs()
    {
        try
        {
            string json = JsonConvert.SerializeObject(promptPresets);
            EditorPrefs.SetString(EDITORPREF_PROMPTS, json);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[GeminiImageGen] 프리셋 저장 실패: " + ex.Message);
        }
    }
    
    private byte[] EncodeTextureSafeToPNG(Texture2D src)
    {
        if (src == null) return null;

        // 1) 가능한 경우: 이미 readable + 비압축 포맷이면 바로 인코딩 시도
        try
        {
            // 일부 환경에서는 readable 여부만 충족하면 압축 포맷에서도 바로 실패하므로,
            // 바로 시도 → 실패 시 아래 Blit 경로로 폴백.
            return src.EncodeToPNG();
        }
        catch (Exception ex)
        {
            // fallthrough
            // 바로 시도해보고 안되면 밑으로 감.
        }

        // 2) 보편 경로: Blit → ReadPixels → RGBA32로 복제 후 Encode
        RenderTexture prev = RenderTexture.active;
        RenderTexture rt = null;
        try
        {
            int w = Mathf.Max(1, src.width);
            int h = Mathf.Max(1, src.height);
            rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);

            Graphics.Blit(src, rt);

            RenderTexture.active = rt;
            Texture2D readable = new Texture2D(w, h, TextureFormat.RGBA32, false, false);
            readable.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            readable.Apply(false, false);

            byte[] png = readable.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(readable);
            return png;
        }
        catch (Exception ex)
        {
            Debug.LogError("[GeminiImageGen] EncodeTextureSafeToPNG 실패: " + ex.Message);
            return null;
        }
        finally
        {
            RenderTexture.active = prev;
            if (rt != null) RenderTexture.ReleaseTemporary(rt);
        }
    }
}
