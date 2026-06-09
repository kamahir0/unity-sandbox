#if UNITY_EDITOR
using System;
using System.Threading;
using System.Threading.Tasks;
using UniCli.Protocol;
using UniCli.Server.Editor.Handlers;

namespace UxmlToUgui
{
    [Serializable]
    public class UxmlToUguiRequest
    {
        public string uxmlPath = "";
        public string outputPath = "";
        public float fontScale = 1.0f;
    }

    [Serializable]
    public class UxmlToUguiResponse
    {
        public bool success;
        public string message = "";
        public int nodeCount;
        public int warnCount;
        public int todoCount;
    }

    public sealed class UxmlToUguiCommandHandler : CommandHandler<UxmlToUguiRequest, UxmlToUguiResponse>
    {
        public override string CommandName => "Uxml.ToUgui";
        public override string Description => "Convert a UXML file to a UGUI Prefab with optional font scaling.";

        protected override ValueTask<UxmlToUguiResponse> ExecuteAsync(UxmlToUguiRequest request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(request.uxmlPath))
            {
                return new ValueTask<UxmlToUguiResponse>(new UxmlToUguiResponse
                {
                    success = false,
                    message = "uxmlPath is required."
                });
            }

            if (string.IsNullOrEmpty(request.outputPath))
            {
                request.outputPath = System.IO.Path.ChangeExtension(request.uxmlPath, ".prefab");
            }

            try
            {
                var result = UxmlToUguiConverter.Convert(request.uxmlPath, request.outputPath, request.fontScale);
                if (result == null)
                {
                    return new ValueTask<UxmlToUguiResponse>(new UxmlToUguiResponse
                    {
                        success = false,
                        message = "Conversion failed (check Unity logs for XML/file load errors)."
                    });
                }

                return new ValueTask<UxmlToUguiResponse>(new UxmlToUguiResponse
                {
                    success = true,
                    message = $"Converted successfully to {request.outputPath}",
                    nodeCount = result.NodeCount,
                    warnCount = result.WarnCount,
                    todoCount = result.TodoCount
                });
            }
            catch (Exception e)
            {
                return new ValueTask<UxmlToUguiResponse>(new UxmlToUguiResponse
                {
                    success = false,
                    message = $"Unexpected error: {e.Message}\n{e.StackTrace}"
                });
            }
        }
    }
}
#endif
