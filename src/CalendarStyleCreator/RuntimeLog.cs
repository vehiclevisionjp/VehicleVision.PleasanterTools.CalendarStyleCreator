namespace VehicleVision.PleasanterTools.CalendarStyleCreator;

/// <summary>コンソール出力は英語固定。例外メッセージや資格情報は出さない。</summary>
public static class RuntimeLog
{
    public static void WriteLine(string message) => Console.Out.WriteLine(message);
    public static void WriteError(string message) => Console.Error.WriteLine(message);
}
