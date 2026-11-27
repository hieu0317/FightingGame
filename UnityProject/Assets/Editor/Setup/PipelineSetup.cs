using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace LoanDau.EditorSetup
{
    /// <summary>
    /// Chuyển project sang URP (Built-in Render Pipeline đã bị khai tử từ Unity 6.5).
    /// Tạo Assets/Settings/URP_Pipeline.asset + URP_Renderer.asset, gán làm pipeline mặc định cho mọi
    /// mức Quality. Chạy lại bao nhiêu lần cũng được.
    /// Menu: Loan Dau/Setup/Chuyển sang URP · Batch: -executeMethod LoanDau.EditorSetup.PipelineSetup.RunBatch
    /// </summary>
    public static class PipelineSetup
    {
        const string Folder = "Assets/Settings";
        const string PipelinePath = Folder + "/URP_Pipeline.asset";
        const string RendererPath = Folder + "/URP_Renderer.asset";

        [MenuItem("Loan Dau/Setup/Chuyển sang URP")]
        public static void Run()
        {
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets", "Settings");

            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (!pipeline)
            {
                var renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(renderer, RendererPath);
                pipeline = UniversalRenderPipelineAsset.Create(renderer);
                AssetDatabase.CreateAsset(pipeline, PipelinePath);
                Debug.Log($"[Setup] Tạo {PipelinePath}");
            }

            GraphicsSettings.defaultRenderPipeline = pipeline;
            int current = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = pipeline;
            }
            QualitySettings.SetQualityLevel(current, false);

            AssetDatabase.SaveAssets();
            Debug.Log($"[Setup] Render pipeline: {GraphicsSettings.defaultRenderPipeline?.name}");
        }

        public static void RunBatch()
        {
            Run();
            EditorApplication.Exit(GraphicsSettings.defaultRenderPipeline is UniversalRenderPipelineAsset ? 0 : 1);
        }
    }
}
