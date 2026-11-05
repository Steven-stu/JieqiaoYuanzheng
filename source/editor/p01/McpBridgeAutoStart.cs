#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using MCPForUnity.Editor;

/// <summary>
/// Starts the MCP for Unity stdio bridge as soon as a domain loads.
///
/// The package leaves the bridge stopped until its window is opened, so an
/// editor that is merely *running* is invisible to the Python MCP server
/// ("No Unity Editor instances found"). McpCiBoot.StartStdioForCi() is the
/// package's own entrypoint for exactly this, so we call it on every reload
/// and the bridge is up whenever this project is open.
/// </summary>
[InitializeOnLoad]
public static class McpBridgeAutoStart
{
    static McpBridgeAutoStart()
    {
        // delayCall: the bridge needs a fully loaded domain before it can listen.
        EditorApplication.delayCall += () =>
        {
            try
            {
                McpCiBoot.StartStdioForCi();
                Debug.Log("[McpBridgeAutoStart] MCP for Unity stdio bridge start requested.");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[McpBridgeAutoStart] Could not start MCP bridge: {e.Message}");
            }
        };
    }
}
#endif
