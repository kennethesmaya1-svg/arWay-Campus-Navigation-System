using UnityEngine;
using System.IO;
using System;

public class SessionLogger : MonoBehaviour
{
    private string logFilePath;
    private StreamWriter streamWriter;

    void Awake()
    {
        // Keep this logger alive even if we change scenes
        DontDestroyOnLoad(gameObject);

        // Create a unique file name using the exact time the app was opened
        string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        
        // Save it to the phone's persistent data folder
        logFilePath = Path.Combine(Application.persistentDataPath, "AppLog_" + timestamp + ".txt");

        // Open the text file
        streamWriter = new StreamWriter(logFilePath, true);
        streamWriter.AutoFlush = true;

        // Tell Unity to send every Debug.Log to our custom function
        Application.logMessageReceived += HandleLog;
        
        Debug.Log("Session Logger Started. Saving to: " + logFilePath);
    }

    void HandleLog(string logString, string stackTrace, LogType type)
    {
        if (streamWriter != null)
        {
            // Write the time, the type, and the message
            streamWriter.WriteLine("[" + DateTime.Now.ToString("HH:mm:ss") + "] [" + type.ToString() + "] " + logString);
            
            // If the app throws a red error, print the stack trace too so we can debug it
            if (type == LogType.Error || type == LogType.Exception)
            {
                streamWriter.WriteLine(stackTrace);
            }
        }
    }

    void OnDestroy()
    {
        Application.logMessageReceived -= HandleLog;
        if (streamWriter != null)
        {
            streamWriter.Close();
        }
    }
}