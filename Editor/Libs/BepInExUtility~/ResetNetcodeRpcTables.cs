#if UNITY_EDITOR
using System.Collections;
using Unity.Netcode;
using UnityEngine;

// ? eat my socks Unity and your lack of using domain reload
public static class ResetNetcodeRpcTables {
    public static bool DidReset;
    
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void OnBeforeSceneLoadRuntimeMethod() {
        if (DidReset) return;
        ResetRpcFuncTable();
    }

    public static void ResetRpcFuncTable() {
        if (DidReset) return;
        
#if UNITY_EDITOR
        if (!UnityEditor.EditorSettings.enterPlayModeOptionsEnabled ||
            !UnityEditor.EditorSettings.enterPlayModeOptions.HasFlag(UnityEditor.EnterPlayModeOptions.DisableDomainReload)) {
            return;
        }
#endif
        var rpcFuncTableField = typeof(NetworkManager).GetField("__rpc_func_table");
        var rpcNameTableField = typeof(NetworkManager).GetField("__rpc_name_table");
        var rpcFuncTable = (IDictionary)rpcFuncTableField.GetValue(null);
        var rpcNameTable = (IDictionary)rpcNameTableField.GetValue(null);
        rpcFuncTable.Clear();
        rpcNameTable.Clear();
        rpcFuncTableField.SetValue(null, rpcFuncTable);
        rpcNameTableField.SetValue(null, rpcNameTable);
        Debug.Log("Reset rpc_func_table and rpc_name_table.");
        DidReset = true;
    }
}
#endif