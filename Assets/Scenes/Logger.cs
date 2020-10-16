using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// 日志记录器
/// </summary>
public class Logger : Singleton<Logger>
{
    /// <summary>
    /// 存储日志等级(0普通,1警告,2错误)
    /// </summary>
    public bool LogSwitch = false; //日志功能开关，可在程序中开、关日志

    private readonly object _enqueneLocker = new object();
    private readonly object _dequeneLocker = new object();
    private Queue<LogItem> _logQueue;     //日志队列
    private string systemLogFolder;     //系统日志文件
    private string webServerLogFolder;  //WebServer日志文件
    private string webSocketLogFolder;  //WebSocket日志文件
    private FileInfo _systemLogFileInfo;    //系统日志文件
    private FileInfo _webServerFilelInfo;   //webserver日志文件信息
    private FileInfo _webSocketFileInfo;    //socket日志文件
    private DateTime _lastCreateFileTime;   //上次创建日志文件的时间
    private Dictionary<LogType, LogLevel> _logTypeLevelDict = null;
    private LogItem _logItemToRecord;   //当前用于记录的日志Item
    private StreamWriter _logStreamWriter;   //用于日志写入
    private StringBuilder strBuilder = new StringBuilder();

    public static Logger Instance
    {
        get
        {
            return ((Logger)mInstance);
        }
        set
        {
            mInstance = value;
        }
    }

    private void Awake()
    {
        if (!LogSwitch)
        {
            return;
        }
        Init();
    }

    private void Update()
    {
        //轮询队列输出日志
        _logItemToRecord = GetLogItemFromQueue();
        if (_logItemToRecord != null)
        {
            RecordLog(_logItemToRecord);
        }
    }

    private void OnDisable()
    {
        //记录程序关闭的时间
        StreamWriter sw;
        string appEndStr = string.Format("{0}{1}{2}", "--------------------------程序关闭--------------------------", Environment.NewLine, Environment.NewLine);
        if (_systemLogFileInfo != null)
        {
            sw = File.Exists(_systemLogFileInfo.FullName) ? _systemLogFileInfo.AppendText() : _systemLogFileInfo.CreateText();
            WriteLog(sw, DateTime.Now, LogLevel.Info, appEndStr);
        }
        if (_webServerFilelInfo != null)
        {
            sw = File.Exists(_webServerFilelInfo.FullName) ? _webServerFilelInfo.AppendText() : _webServerFilelInfo.CreateText();
            WriteLog(sw, DateTime.Now, LogLevel.Info, appEndStr);
        }
        if (_webSocketFileInfo != null)
        {
            sw = File.Exists(_webSocketFileInfo.FullName) ? _webSocketFileInfo.AppendText() : _webSocketFileInfo.CreateText();
            WriteLog(sw, DateTime.Now, LogLevel.Info, appEndStr);
        }
    }

    /// <summary>
    /// 获取日志项
    /// </summary>
    /// <param name="queue"></param>
    /// <returns></returns>
    private LogItem GetLogItemFromQueue()
    {
        if (_logQueue != null)
        {
            if (_logQueue.Count > 0)
            {
                lock (_dequeneLocker)
                {
                    return _logQueue.Dequeue();
                }
            }
        }
        return null;
    }

    /// <summary>
    /// 日志模块初始化
    /// </summary>
    private void Init()
    {
        _logQueue = new Queue<LogItem>();
        _logTypeLevelDict = new Dictionary<LogType, LogLevel>() {
                { LogType.Log, LogLevel.Info },
                { LogType.Warning, LogLevel.WARNING },
                { LogType.Assert, LogLevel.ERROR },
                { LogType.Error, LogLevel.ERROR },
                { LogType.Exception, LogLevel.Exception }
        };
        // 创建文件
        InitLogFiles();
        // 注册回调
        Application.logMessageReceived += UnityBuiltInLogCallback;
        Debug.Log("日志模块启动成功");
    }

    /// <summary>
    /// 创建日志目录
    /// </summary>
    private void InitLogFiles()
    {
        string logFilePath = Environment.CurrentDirectory;
#if UNITY_EDITOR
        systemLogFolder = logFilePath + "/Logs/System";
        webServerLogFolder = logFilePath + "/Logs/WebServer/";
        webSocketLogFolder = logFilePath + "/Logs/WebSocket/";
#else
        systemLogFolder = "/Logs/";
        webServerLogFolder = "/Logs/WebServer/";
        webSocketLogFolder = "/Logs/WebSocket/";
#endif
        //创建目录
        if (!Directory.Exists(systemLogFolder))
        {
            Directory.CreateDirectory(systemLogFolder);
        }
        if (!Directory.Exists(webServerLogFolder))
        {
            Directory.CreateDirectory(webServerLogFolder);
        }
        if (!Directory.Exists(webSocketLogFolder))
        {
            Directory.CreateDirectory(webSocketLogFolder);
        }
        string systemLogPath = systemLogFolder + "/SystemLog_" + DateTime.Now.ToString("yyyyMMdd_HH") + ".txt";
        string webServerLogPath = webServerLogFolder + "/WebServerLog_" + DateTime.Now.ToString("yyyyMMdd_HH") + ".txt";
        string webSocketLogPath = webSocketLogFolder + "/WebSocketLog_" + DateTime.Now.ToString("yyyyMMdd_HH") + ".txt";
        string appStartStr = "--------------------------程序启动--------------------------";
        DateTime time = DateTime.Now;
        //系统日志
        _systemLogFileInfo = new FileInfo(systemLogPath);
        var streamWriter = File.Exists(systemLogPath) ? _systemLogFileInfo.AppendText() : _systemLogFileInfo.CreateText();
        WriteLog(streamWriter, time, LogLevel.Info, appStartStr);
        streamWriter.Close();
        //Web服务日志
        _webServerFilelInfo = new FileInfo(webServerLogPath);
        streamWriter = File.Exists(webServerLogPath) ? _webServerFilelInfo.AppendText() : _webServerFilelInfo.CreateText();
        WriteLog(streamWriter, time, LogLevel.Info, appStartStr);
        streamWriter.Close();
        //Socket日志
        _webSocketFileInfo = new FileInfo(webSocketLogPath);
        streamWriter = File.Exists(webSocketLogPath) ? _webSocketFileInfo.AppendText() : _webSocketFileInfo.CreateText();
        WriteLog(streamWriter, time, LogLevel.Info, appStartStr);
        streamWriter.Close();
    }

    /// <summary>
    /// 根据时间段创建日志文件
    /// </summary>
    /// <param name="time"></param>
    private void CreateLogFileByTime(DateTime time)
    {
        string systemLogPath = systemLogFolder + "/SystemLog_" + time.ToString("yyyyMMdd_HH") + ".txt";
        string webServerLogPath = webServerLogFolder + "/WebServerLog_" + time.ToString("yyyyMMdd_HH") + ".txt";
        string webSocketLogPath = webSocketLogFolder + "/WebSocketLog_" + time.ToString("yyyyMMdd_HH") + ".txt";
        if (!File.Exists(systemLogPath))
        {
            File.Create(systemLogPath);
            _systemLogFileInfo = new FileInfo(systemLogPath);
        }
        if (!File.Exists(webServerLogPath))
        {
            File.Create(webServerLogPath);
            _webServerFilelInfo = new FileInfo(webServerLogPath);
        }
        if (!File.Exists(webSocketLogPath))
        {
            File.Create(webSocketLogPath);
            _webSocketFileInfo = new FileInfo(webSocketLogPath);
        }
    }

    /// <summary>
    /// 记录日志
    /// </summary>
    /// <param name="dt"></param>
    private void RecordLog(LogItem logItem)
    {
        if (LogSwitch && logItem != null)
        {
            IsNeedCreateAnotherFile();
            switch (logItem.Level)
            {
                case LogLevel.Debug:
                case LogLevel.Info:
                case LogLevel.WARNING:
                case LogLevel.ERROR:
                case LogLevel.Exception:
                    {
                        _logStreamWriter = File.Exists(_systemLogFileInfo.FullName) ? _systemLogFileInfo.AppendText() : _systemLogFileInfo.CreateText();
                        break;
                    }
                case LogLevel.Request:
                case LogLevel.Response:
                    {
                        _logStreamWriter = File.Exists(_webServerFilelInfo.FullName) ? _webServerFilelInfo.AppendText() : _webServerFilelInfo.CreateText();
                        break;
                    }
                case LogLevel.Socket:
                    {
                        _logStreamWriter = File.Exists(_webSocketFileInfo.FullName) ? _webSocketFileInfo.AppendText() : _webSocketFileInfo.CreateText();
                        break;
                    }
            }
            WriteLog(_logStreamWriter, logItem.Time, logItem.Level, logItem.MessageString, logItem.StackTrace);
        }
    }

    /// <summary>
    /// 在日志文件中写日志内容
    /// </summary>
    /// <param name="streamWriter"></param>
    /// <param name="time"></param>
    /// <param name="level"></param>
    /// <param name="message"></param>
    /// <param name="stackTrace"></param>
    private void WriteLog(StreamWriter streamWriter, DateTime time, LogLevel level, string message, string stackTrace = null)
    {
        if (streamWriter != null)
        {
            try
            {
                string logStr;
                if (!string.IsNullOrEmpty(stackTrace) && (level == LogLevel.ERROR || level == LogLevel.Exception))
                {
                    logStr = string.Format("[{0}] [{1}] {2,-12} - {3}\r\nStackTrace ==> {4}", time.ToString("yyyy-MM-dd"), time.ToString("HH:mm:ss.ff"), "[" + level + "]", message, stackTrace.Trim());
                }
                else
                {
                    logStr = string.Format("[{0}] [{1}] {2,-12} - {3}", time.ToString("yyyy-MM-dd"), time.ToString("HH:mm:ss.ff"), "[" + level + "]", message);
                }
                streamWriter.WriteLine(logStr);
                streamWriter.Close();
            }
            catch (IOException ex)
            {
                //Debug.Log(ex.Message);
            }
        }
    }

    /// <summary>
    /// Unity内置Log的回调
    /// </summary>
    /// <param name="condition">日志信息</param>
    /// <param name="stackTrace">堆栈信息</param>
    /// <param name="type">unity内置日志类型</param>
    private void UnityBuiltInLogCallback(string condition, string stackTrace, LogType type)
    {
        LogEnqueue(condition, _logTypeLevelDict[type], stackTrace);
    }

    /// <summary>
    /// 日志入队
    /// </summary>
    /// <param name="condition"></param>
    /// <param name="level"></param>
    /// <param name="stackTrace"></param>
    private void LogEnqueue(string condition, LogLevel level, string stackTrace = null)
    {
        lock (_enqueneLocker)
        {
            _logQueue.Enqueue(new LogItem()
            {
                MessageString = condition,
                StackTrace = stackTrace,
                Level = level,
                Time = DateTime.Now
            });
        }
    }

    /// <summary>
    /// 是否创建另一个日志文件
    /// </summary>
    private void IsNeedCreateAnotherFile()
    {
        if (_lastCreateFileTime != null)
        {//按时间判断日志文件，小时区分
            var time = DateTime.Now;
            if (!_lastCreateFileTime.ToString("yyyyMMddHH").Equals(time.ToString("yyyyMMddHH")))
            {
                //新建目录
                CreateLogFileByTime(DateTime.Now);
                _lastCreateFileTime = time;
            }
        }
    }

    #region 外部调用
    /// <summary>
    /// 外部调用日志开关
    /// </summary>
    /// <param name="openOff">true:开;false:关</param>
    public void OpenOffLog(bool openOff)
    {
        if (openOff)
        {//开日志
            if (!LogSwitch)
            {
                Init();
            }
        }
        else //关日志
        {
            if (LogSwitch)
            {
                LogSwitch = false;
                Application.logMessageReceived -= UnityBuiltInLogCallback;
            }
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="context"></param>
    /// <param name="level"></param>
    public void RecordLog(string context, LogLevel level)
    {
        LogEnqueue(context, level);
    }

    #endregion
}
/// <summary>
/// 日志类型
/// </summary>
public enum LogLevel
{
    /// <summary>
    /// 调试日志
    /// </summary>
    Debug = 0,
    /// <summary>
    /// 普通日志
    /// </summary>
    Info = 1,
    /// <summary>
    /// WebApi请求日志
    /// </summary>
    Request = 2,
    /// <summary>
    /// WebApi返回日志
    /// </summary>
    Response = 3,
    /// <summary>
    /// Socket接收日志
    /// </summary>
    Socket = 4,
    /// <summary>
    /// 报警日志
    /// </summary>
    WARNING = 5,
    /// <summary>
    /// 错误日志
    /// </summary>
    ERROR = 6,
    /// <summary>
    /// 异常日志
    /// </summary>
    Exception = 7

}
/// <summary>
/// 日志结构体
/// </summary>
public class LogItem
{
    /// <summary>
    /// 日志内容
    /// </summary>
    public string MessageString { get; set; }
    /// <summary>
    /// 调用堆栈
    /// </summary>
    public string StackTrace { get; set; }
    /// <summary>
    /// 日志类型
    /// </summary>
    public LogLevel Level { get; set; }
    /// <summary>
    /// 记录时间
    /// </summary>
    public DateTime Time { get; set; }
}