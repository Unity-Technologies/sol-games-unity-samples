using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine.Windows;
using System.Text;
using System;
using System.Diagnostics;
using Debug = UnityEngine.Debug;

public class MaliShaderReport : EditorWindow
{
    static class Content
    {
        public static readonly GUIContent targetContent = new GUIContent("Shader", "Shader to be Analyzed.");
        public static readonly GUIContent compilerContent = new GUIContent("Mali Compiler", "Compiler to use");

        public static readonly GUIContent allKeywords = new GUIContent("All Keywords", "All keywords that can be used in this shader");

        public static readonly GUIContent[] compileOptions = new GUIContent[] {
        new GUIContent("Arm Studio - Malioc (PATH)", "Mali Offline Compiler Arm Studio"),
        new GUIContent("Offline Compiler - Malisc (PATH)", "Mali Offline Compiler Legacy Install"),
        new GUIContent("Custom Location", "Custom Location")
        };

        public static readonly GUIContent[] shaderTypes = new GUIContent[] {
        new GUIContent("Vertex Shader", "Vertex shader report"),
        new GUIContent("Geometry Shader", "Geometry shader report"),
        new GUIContent("Fragment Shader", "Fragment shader report"),
    };
    }

    // NOTE: Keep this the same as Content.shaderTypes
    private readonly ShaderType[] k_ReportOrder = new ShaderType[] { ShaderType.Vertex, ShaderType.Geometry, ShaderType.Fragment };

    // Compilers supported
    enum CompilerTarget
    {
        Malioc, // ARM Studio (Latest) Mali Offline compiler (https://developer.arm.com/tools-and-software/graphics-and-gaming/arm-mobile-studio/components/mali-offline-compiler)
        Malisc, // ARM Mali Compiler Legacy (6.4 and lower) (https://developer.arm.com/tools-and-software/graphics-and-gaming/mali-offline-compiler/downloads)
        Custom, // Just point to the executable to use
    }

    // Compiler Info
    private CompilerTarget m_Compiler = CompilerTarget.Malioc;
    private string m_CompilerPath;

    // Shader and pass
    private Shader m_Target = null;
    private int m_SelectedPass = -1;

    // Keywords and pass names
    private List<string> m_Keywords = new List<string>();
    private string[] m_PassNames = null;

    // Scrolling
    Vector2 m_KeywordScroll = new Vector2();
    Vector2 m_PassKeywordScroll = new Vector2();
    Vector2 m_ReportScroll = new Vector2();

    // Message for when a combination has no keys applied
    private const string k_NA = "No keyword applied";

    private const int k_MaxHintLength = 18;

    // Simple class to contain pass information
    // this is then shown to the user via the pass dropdown
    class PassInfo
    {
        public int subShader;
        public List<List<string>> keywords;
        public List<string> keywordHint;
        public string name;
               
        public ShaderData.Pass pass;


        public PassInfo()
        {
            subShader = 0;
            keywords = new List<List<string>>();
            keywordHint = new List<string>();
            name = "";
            pass = null;
        }

    }

    private List<PassInfo> m_Passes = new List<PassInfo>();

    private PassInfo defaultPassInfo = new PassInfo();
    private List<int> m_SelectedKeywords = new List<int>();

    private StringBuilder m_CompilerOutput = new StringBuilder();
    private int m_lines = 0;

    private Dictionary<ShaderType, string> m_Report = new Dictionary<ShaderType, string>();



    // Add menu named "My Window" to the Window menu
    [MenuItem("Shader/Mali Offline Compiler")]
    static void Init()
    {
        // Get existing open window or if none, make a new one:
        MaliShaderReport window = (MaliShaderReport)EditorWindow.GetWindow(typeof(MaliShaderReport));
        window.Show();
    }

    void OnGUI()
    {
        // Choose the compiler
        m_Compiler = (CompilerTarget)EditorGUILayout.Popup(Content.compilerContent, (int)m_Compiler, Content.compileOptions);
        if(m_Compiler == CompilerTarget.Custom) 
        {
            EditorGUILayout.BeginHorizontal();
            m_CompilerPath = EditorGUILayout.TextField(m_CompilerPath);
            if (GUILayout.Button("...")) {
#if UNITY_STANDALONE_WIN
                m_CompilerPath = EditorUtility.OpenFilePanel("Mali Offline Compiler", "", "exe");
#else
                m_CompilerPath = EditorUtility.OpenFilePanel("Mali Offline Compiler", "", "");
#endif
            }
            EditorGUILayout.EndHorizontal();
        }

        // Select the shader
        Shader s = EditorGUILayout.ObjectField(Content.targetContent, m_Target, typeof(Shader), true) as Shader;

        // Go through and setup the data needed to compile a shader
        // we only do this once when a shader is selected.
        ProcessShader(s);


        // Display the keywords
        {
            GUILayout.Label(Content.allKeywords, EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical(GUILayout.MaxHeight(40.0f));
            m_KeywordScroll = EditorGUILayout.BeginScrollView(m_KeywordScroll);
            EditorGUILayout.BeginHorizontal();
            for (int i = 0; i < m_Keywords.Count; i++) {
                GUILayout.Label(m_Keywords[i], EditorStyles.label);
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }


        // Select Pass to investigate
        GUILayout.Label("Pass:", EditorStyles.boldLabel);
        if (m_PassNames != null) {
            SetSelectedPass( EditorGUILayout.Popup(m_SelectedPass, m_PassNames));
        }


        PassInfo info;

        if(m_SelectedPass>=0 && m_SelectedPass< m_Passes.Count) {
            info = m_Passes[m_SelectedPass];
        }
        else {
            info = defaultPassInfo;
        }

        if (m_SelectedKeywords.Count != info.keywords.Count) {
            SetSelectedPass(m_SelectedPass, true);
        }
        
        EditorGUILayout.BeginVertical();
        // Pass Keyword selection
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Pass Name:");
            GUILayout.Label(info.name);
            EditorGUILayout.EndHorizontal();
            GUILayout.Label("Pass Keywords:");
            m_PassKeywordScroll = EditorGUILayout.BeginScrollView(m_PassKeywordScroll);
            EditorGUILayout.BeginVertical();
            for (int i = 0; i < m_SelectedKeywords.Count; i++) {
                string[] options = info.keywords[i].ToArray();

                if (options.Length == 0)
                    continue;

                if (options[0].Length == 0) {
                    options[0] = k_NA;
                }

                m_SelectedKeywords[i] = EditorGUILayout.Popup(info.keywordHint[i], m_SelectedKeywords[i], options);
            }
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndScrollView();
        }

        // Report section
        {
            m_ReportScroll = EditorGUILayout.BeginScrollView(m_ReportScroll);
            EditorGUILayout.BeginVertical();

            string value = null;
            for (int i = 0; i < k_ReportOrder.Length; i++) {
                if (m_Report.TryGetValue(k_ReportOrder[i], out value)) {
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Label(Content.shaderTypes[i]);
                    if (GUILayout.Button("Copy text")) {
                        EditorGUIUtility.systemCopyBuffer = value;
                    }

                    EditorGUILayout.EndHorizontal();

                    GUILayout.Box(value);
                }
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.EndScrollView();
        }
        EditorGUILayout.EndVertical();
        EditorGUILayout.BeginHorizontal();


        GUI.enabled = info != defaultPassInfo;
        if (GUILayout.Button("Compile & Report")) {
            CompileAndReport();
        }
        GUI.enabled = true;

        if (GUILayout.Button("Refresh Shader")) {
            // reprocess the shader
            ProcessShader(s, true);
        }

        if (GUILayout.Button("Clear Report")) {
            m_Report.Clear();
        }

        EditorGUILayout.EndHorizontal();
    }


    void CompileAndReport()
    {
        ShaderType[] types = new ShaderType[] { ShaderType.Vertex, ShaderType.Geometry, ShaderType.Fragment };

        string shader = FileUtil.GetUniqueTempPathInProject() + ".shader";

        string[] keywords = GenerateKeywords();

        bool bCompiled = CompileShaderToFile(shader, keywords);

        List<ShaderType> supported = new List<ShaderType>();

        for (int i=0; i<types.Length; i++) {
            if (m_Passes[m_SelectedPass].pass.HasShaderStage(types[i])) {
                supported.Add(types[i]);
            }
        }

        Report(shader, supported);
    }

    string[] GenerateKeywords()
    {
        if (m_SelectedKeywords.Count == 0) {
            return new string[0];
        }

        List<string> used = new List<string>();

        for (int i = 0; i < m_SelectedKeywords.Count; i++) {

            string keyword = m_Passes[m_SelectedPass].keywords[i][m_SelectedKeywords[i]];

            if (keyword.Length == 0)
                continue;

            used.Add(keyword);
        }

        return used.ToArray();
    }

    bool CompileShaderToFile(string path, string[] keywords)
    {
        Debug.Log("Compiling " + path + " with "+keywords.Length+" keywords");
        ShaderData.VariantCompileInfo result = m_Passes[m_SelectedPass].pass.CompileVariant(ShaderType.Vertex, keywords, ShaderCompilerPlatform.GLES3x, BuildTarget.Android);


        if (result.Success) {

            // Convert a byte array to a C# string. 
            string code = Encoding.ASCII.GetString(result.ShaderData);

            code = code.Replace("#version 310 es", "").Replace("#version 300 es", "").Insert(0, "#version 310 es\n");

            byte[] data = Encoding.ASCII.GetBytes(code);

            File.WriteAllBytes(path, data);

            //Debug.Log("Compiled, Bytes:"+ result.ShaderData.Length);
            return true;
        }

        Debug.Log("Compile Failed");
        return false;
    }

    string GetCompiler()
    {
        switch (m_Compiler) {
            case CompilerTarget.Malioc: 
                return "malioc.exe";

            case CompilerTarget.Malisc:
                return "malisc.exe";

            case CompilerTarget.Custom:
                return m_CompilerPath;
        }

        return "";
    }
    
    void Report(string path, List<ShaderType> supported)
    {
        m_Report.Clear();
        string log;
        foreach(ShaderType type in supported){
            if(Report(path, type, out log)) {
                m_Report.Add(type, log);
            }
            //Debug.Log(log);
        }
    }

    bool Report(string path, ShaderType target, out string report)
    {
        bool bSuccess = false;
        try {

            m_lines = 0;
            m_CompilerOutput.Clear();

            Process compiler = new Process();

            compiler.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            compiler.StartInfo.CreateNoWindow = true;
            compiler.StartInfo.UseShellExecute = false;
            compiler.StartInfo.FileName = GetCompiler();
            compiler.StartInfo.RedirectStandardOutput = true;

            compiler.OutputDataReceived += Compiler_OutputDataReceived;

            switch (target) {
                case ShaderType.Vertex:
                    compiler.StartInfo.Arguments = "-v " + path + " -D VERTEX";
                    break;
                case ShaderType.Fragment:
                    compiler.StartInfo.Arguments = "-f " + path + " -D FRAGMENT";
                    break;
                case ShaderType.Geometry:
                    compiler.StartInfo.Arguments = "-g " + path + " -D GEOMETRY";
                    break;
            }

            compiler.EnableRaisingEvents = true;
            compiler.Start();
            compiler.BeginOutputReadLine();

            compiler.WaitForExit();


            int ExitCode = compiler.ExitCode;


            report = m_CompilerOutput.ToString();
            bSuccess = true;
        }
        catch (Exception e) {
            report = e.ToString();
            Debug.LogException(e);
        }

        return bSuccess;
    }

    private void Compiler_OutputDataReceived(object sender, DataReceivedEventArgs e)
    {
        if (!String.IsNullOrEmpty(e.Data)) {
            m_lines++;
            m_CompilerOutput.Append(e.Data);
            m_CompilerOutput.Append('\n');
        }
    }

    // TODO: Extract the shader compiled hlsl to glsl for the shader and include that in the report
    void ExtractCode(string path, ShaderType type)
    {

    }



    void ProcessShader(Shader target, bool force =false)
    {
        // Reset
        if(target == null) {
            m_Keywords.Clear();
            m_Passes.Clear();
            m_SelectedPass = -1;
            m_Target = null;
            m_PassNames = null;
        }
        else if( target != m_Target || force) {
            m_Keywords.Clear();
            m_Passes.Clear();

            if (!force) {
                m_SelectedPass = 0;
            }

            m_Target = target;
            int count = target.passCount;

            ShaderData data = ShaderUtil.GetShaderData(target);

            List<string> names = new List<string>();

            StringBuilder sb = new StringBuilder(k_MaxHintLength * 3);

            for(int sub = 0; sub < data.SubshaderCount; sub++) {
                ShaderData.Subshader subshader = data.GetSubshader(sub);
                for(int p = 0; p < subshader.PassCount; p++) {
                    PassInfo passInfo = new PassInfo();
                    passInfo.subShader = sub;
                    passInfo.pass = subshader.GetPass(p);
                    passInfo.name = passInfo.pass.Name;

                    ParseKeywords(passInfo.pass.SourceCode, ref passInfo.keywords);

                    // Create some label hints
                    for(int i=0; i <passInfo.keywords.Count; i++) {

                        for (int j = 0; j < passInfo.keywords[i].Count; j++) {
                            if (passInfo.keywords[i][j].Length > 0) {
                                sb.Append(passInfo.keywords[i][j]);

                                if (sb.Length > k_MaxHintLength) {
                                    sb.Remove(k_MaxHintLength, sb.Length - k_MaxHintLength);
                                    sb[15] = '.';
                                    sb[16] = '.';
                                    sb[17] = '.';

                                    break;
                                }
                                else if (sb.Length == k_MaxHintLength) {
                                    break;
                                }

                            }
                        }

                        passInfo.keywordHint.Add(sb.ToString());
                        sb.Clear();
                    }

                    m_Passes.Add(passInfo);
                    names.Add(passInfo.name);
                }
            }

            if (m_SelectedPass < 0)
                m_SelectedPass = 0;
            else if(m_SelectedPass >= m_Passes.Count) {
                m_SelectedPass = m_Passes.Count - 1;
            }

            SetSelectedPass(m_SelectedPass, true);

            m_PassNames = names.ToArray();
        }
    }

    void SetSelectedPass(int index, bool init = false)
    {
        if(init || index != m_SelectedPass) {
            m_SelectedPass = index;

            m_SelectedKeywords.Clear();


            if(m_SelectedPass >= 0 && m_SelectedPass < m_Passes.Count) {
                PassInfo info = m_Passes[m_SelectedPass];

                for(int i=0; i< info.keywords.Count; i++) {
                    m_SelectedKeywords.Add(0);
                }
            }
            
        }
    }

    void ParseKeywords(
        string code,
        ref List<List<string>> keywords        
        )
    {
        keywords.Clear();

        int pragmaIndex = code.IndexOf("#pragma");

        // TODO: Change this to just use index of the string and avoid this substring nonscence as that just creating unnecessary garbage
        while (pragmaIndex != -1) {
            int endOfLine = code.IndexOf('\n', pragmaIndex);
            ParsePragmaString(code.Substring(pragmaIndex, endOfLine - pragmaIndex), ref keywords);
            code = code.Substring(endOfLine + 1);
            pragmaIndex = code.IndexOf("#pragma");
        }
    }

    void ParsePragmaString(string pragma, ref List<List<string>> outkeywords)
    {
        // Handle a multi_compile and its variants
        int indexStart = pragma.IndexOf("multi_compile");
        if (indexStart != -1) {
            List<string> keys = new List<string>();

            indexStart = pragma.IndexOf(' ', indexStart);

            if (indexStart == -1)
                return;

            pragma = pragma.Substring(indexStart + 1);

            string[] keywords = pragma.Split(' ');

            for(int i=0; i < keywords.Length; i++) {
                if (keywords[i].Equals("_")) {
                    keys.Add("");
                }
                else {
                    keys.Add(keywords[i]);

                    if (!m_Keywords.Contains(keywords[i])) {
                        m_Keywords.Add(keywords[i]);
                    }
                }
            }

            if (keys.Count > 0) {
                outkeywords.Add(keys);
            }
        }
        // Handle a shader_feature and its variants
        else {
            indexStart = pragma.IndexOf("shader_feature");
            if (indexStart != -1) {
                List<string> keys = new List<string>();

                indexStart = pragma.IndexOf(' ', indexStart);

                if (indexStart == -1)
                    return;

                pragma = pragma.Substring(indexStart + 1);

                string[] keywords = pragma.Split(' ');

                for (int i = 0; i < keywords.Length; i++) {
                    if (keywords[i].Equals("_")) {
                        keys.Add("");
                    }
                    else {
                        keys.Add(keywords[i]);
                        if (!m_Keywords.Contains(keywords[i])) {
                            m_Keywords.Add(keywords[i]);
                        }
                    }
                }

                if(keywords.Length == 1) {
                    keys.Insert(0, "");
                }

                if (keys.Count > 0) {
                    outkeywords.Add(keys);
                }
            }
        }
    }

}
