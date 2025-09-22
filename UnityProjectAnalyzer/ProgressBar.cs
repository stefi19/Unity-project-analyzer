using System;
using System.Threading;

namespace UnityProjectAnalyzer
{
    public static class ProgressBar
    {
        public static void Show(int current, int total, string operation = "Processing")
        {
            if (total == 0) return;
            
            var percentage = (double)current / total;
            var barLength = 40;
            var filledLength = (int)(barLength * percentage);
            
            var bar = new string('█', filledLength) + new string('░', barLength - filledLength);
            
            Console.Write($"\r{operation}: [{bar}] {current}/{total} ({percentage:P0})");
            
            if (current == total)
            {
                Console.WriteLine(" Complete");
            }
        }
        
        public static void ShowSpinner(string message)
        {
            var spinnerChars = new char[] { '⠋', '⠙', '⠹', '⠸', '⠼', '⠴', '⠦', '⠧', '⠇', '⠏' };
            var counter = 0;
            
            while (true)
            {
                Console.Write($"\r{spinnerChars[counter % spinnerChars.Length]} {message}");
                Thread.Sleep(100);
                counter++;
                
                if (Console.KeyAvailable)
                    break;
            }
        }
        
        public static void ShowSuccess(string message)
        {
            Console.WriteLine($"[SUCCESS] {message}");
        }
        
        public static void ShowWarning(string message)
        {
            Console.WriteLine($"[WARNING] {message}");
        }
        
        public static void ShowError(string message)
        {
            Console.WriteLine($"[ERROR] {message}");
        }
        
        public static void ShowInfo(string message)
        {
            Console.WriteLine($"[INFO] {message}");
        }
    }
    
    public class PerformanceTimer : IDisposable
    {
        private readonly DateTime _startTime;
        private readonly string _operationName;
        
        public PerformanceTimer(string operationName)
        {
            _operationName = operationName;
            _startTime = DateTime.Now;
            Console.WriteLine($"Starting: {operationName}");
        }
        
        public void Dispose()
        {
            var elapsed = DateTime.Now - _startTime;
            Console.WriteLine($"Completed: {_operationName} in {elapsed.TotalMilliseconds:F1}ms");
        }
    }
}