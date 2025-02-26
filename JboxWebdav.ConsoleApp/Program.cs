﻿using System.Net;
using NWebDav.Server;
using NWebDav.Server.Http;
using NWebDav.Server.HttpListener;
using NWebDav.Server.Logging;
using NWebDav.Server.Stores;
using NWebDav.Sample.HttpListener.LogAdapters;
using Jbox.Service;
using NWebDav.Server.Helpers;
using System.Text;
using JboxWebdav.Server.Jbox;

namespace NWebDav.Sample.HttpListener
{
    internal class Program
    {
        private static ILogger s_log = LoggerFactory.CreateLogger(typeof(Program));
        public static WebDavDispatcher webDavDispatcher;
        public static string Address;

        private static async void DispatchHttpRequestsAsync(System.Net.HttpListener httpListener, CancellationToken cancellationToken)
        {
            var requestHandlerFactory = new RequestHandlerFactory();

            webDavDispatcher = new WebDavDispatcher(new JboxStore(), requestHandlerFactory);

            httpListener.BeginGetContext(new AsyncCallback(GetContextCallBack), httpListener);
        }

        private static async void GetContextCallBack(IAsyncResult ar)
        {
            try
            {
                System.Net.HttpListener httpListener = ar.AsyncState as System.Net.HttpListener;
                if (httpListener.IsListening)
                {
                    HttpListenerContext httpListenerContext = httpListener.EndGetContext(ar);
                    httpListener.BeginGetContext(new AsyncCallback(GetContextCallBack), httpListener);

                    if (httpListenerContext == null)
                        return;

                    IHttpContext httpContext = null;
                    try
                    {
                        httpContext = new HttpContext(httpListenerContext);
                        //httpContext = new HttpBasicContext(httpListenerContext, Jac.checkJac);
                    }
                    catch (Exception ex)
                    {
                        httpContext = new HttpContext(httpListenerContext);
                        httpContext.Response.SetStatus(DavStatusCode.Unauthorized);
                        await httpContext.CloseAsync().ConfigureAwait(false);
                        return;
                    }

                    // Dispatch the request
                    await webDavDispatcher.DispatchRequestAsync(httpContext).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                s_log.Log(LogLevel.Error, () => ex.Message);
                //Console.WriteLine(ex.Message);
                //throw new ArgumentException(ex.Message);
            }
        }

        private static void Main(string[] args)
        {
            Console.WriteLine("Welcome to JboxWebdav!");
            Login();

            LoggerFactory.Factory = new ConsoleAdapter();
            Address = "http://127.0.0.1:65472/";

            while(true)
            {
                using (var httpListener = new System.Net.HttpListener())
                {

                    var cancellationTokenSource = new CancellationTokenSource();
                    try
                    {
                        httpListener.Prefixes.Add(Address);
                        httpListener.AuthenticationSchemes = AuthenticationSchemes.Anonymous;
                        httpListener.Start();

                        DispatchHttpRequestsAsync(httpListener, cancellationTokenSource.Token);

                        Console.WriteLine("WebDAV 服务器运行中。按下 x 退出，按下 c 进入设置。");
                        Console.WriteLine($"监听地址：{Address}");
                    }
                    catch(Exception ex)
                    {
                        s_log.Log(LogLevel.Fatal, () => ex.Message);
                        Console.WriteLine("出现严重错误，请更改正确的监听地址、确保程序有相关权限，然后重启程序！");
                    }
                    

                    while (true)
                    {
                        var key = Console.ReadKey();
                        if (key.KeyChar == 'x')
                        {
                            cancellationTokenSource.Cancel();
                            return;
                        }
                        if (key.KeyChar == 'c')
                        {
                            if (ChangeConfig())
                            {
                                cancellationTokenSource.Cancel();
                                break;
                            }
                        }
                    }
                }
            }
        }

        private static void Login()
        {
            Console.WriteLine("尝试登录...");
            var loginres = Jac.LoginState.success;
            //Jac.InitStorage(new WinStorage());
            Jac.ReadInfo();
            if (Jac.dic.Count > 0)
            {
                var ac = Jac.dic.Keys.First();
                if (Jac.TryLastCookie(ac))
                {
                    Console.WriteLine("Cookie登录成功。");
                    loginres = Jac.LoginState.success;
                    return;
                }
            }
            if (Jac.CheckVPN())
                loginres = Jac.LoginState.fail;
            else
                loginres = Jac.LoginState.novpn;

            if (loginres == Jac.LoginState.novpn)
            {
                while(true)
                {
                    Console.WriteLine("请先拨通交大VPN，然后按任意键继续！");
                    Console.ReadKey(true);
                    if (Jac.CheckVPN())
                        break;
                }
                if (Jac.dic.Count > 0)
                {
                    var ac = Jac.dic.Keys.First();
                    if (Jac.TryLastCookie(ac))
                    {
                        Console.WriteLine("Cookie登录成功。");
                        loginres = Jac.LoginState.success;
                        return;
                    }
                }
            }

            Console.WriteLine("Cookie登录失败，请使用账号密码重新登录。");
            while (true)
            {
                Console.Write("Jaccount账号：");
                var account = Console.ReadLine();
                Console.Write("Jaccount密码：");
                var password = ReadPassword();
                var res1 = Jac.Login(account, password);
                if (res1.state == Jac.LoginState.success)
                {
                    Console.WriteLine("登录成功！");
                    break;
                }
                else
                {
                    Console.WriteLine(res1.message);
                }
            }
        }

        private static string ReadPassword()
        {
            char cPassword;     //登陆时要用的密码 
            StringBuilder cPass = new StringBuilder();//关键方法 
            cPassword = Console.ReadKey(true).KeyChar;//输入字符可以让他不显示出来
            while (cPassword != '\r')//回车
            {
                if (cPassword == '\b')//退格
                {
                    if (cPass.Length == 0)
                    {
                        cPassword = (char)Console.ReadKey(true).KeyChar;//输入字符可以让他不显示出来
                    }
                    else
                    {
                        cPass.Remove(cPass.Length - 1, 1);
                        int cur_x = Console.CursorLeft;
                        int cur_y = Console.CursorTop;
                        Console.SetCursorPosition(cur_x - 1, cur_y);//光标定位    根据光标位置自己改动   x,y坐标
                        Console.Write(" ");
                        Console.SetCursorPosition(cur_x - 1, cur_y);//光标定位    根据光标位置自己改动
                        cPassword = (char)Console.ReadKey(true).KeyChar;
                    }
                }
                else
                {
                    cPass.Append(cPassword);
                    Console.Write('*');
                    cPassword = (char)Console.ReadKey(true).KeyChar;
                }
            }
            Console.WriteLine();
            return cPass.ToString();
        }

        private static bool ChangeConfig()
        {
            var changed = false;
            while(true)
            {
                Console.WriteLine();
                Console.WriteLine("---设置---");
                Console.WriteLine($"1.更改监听地址（当前：{Address}）");
                Console.WriteLine($"2.停用/启用“交大空间”（当前：{(Config.PublicEnabled ? "已启用" : "未启用")}）");
                Console.WriteLine($"3.停用/启用“他人的分享链接”（当前：{(Config.SharedEnabled ? "已启用" : "未启用")}）");
                Console.Write("请输入数字1~3更改设置，留空退出 > ");
                var line = Console.ReadLine();
                switch(line)
                {
                    case "":
                        Console.WriteLine("退出设置。");
                        //changed = true;
                        return changed;
                        break;
                    case "1":
                        Console.Write("请输入新地址，留空不变 > ");
                        var newaddr = Console.ReadLine();
                        if (newaddr == "")
                        {
                            break;
                        }
                        else
                        {
                            Address = newaddr;
                            changed = true;
                        }
                        break;
                    case "2":
                        Config.PublicEnabled = !Config.PublicEnabled;
                        //changed = true;
                        break;
                    case "3":
                        Config.SharedEnabled = !Config.SharedEnabled;
                        //changed = true;
                        break;
                }
            }
        }
    }
}
