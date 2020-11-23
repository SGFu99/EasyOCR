using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace EasyOCR
{

    public partial class Form1 : Form
    {
        OcrTools ocrTools;
        bool ocr_init_flag = false;

        //Resources.resx文件中dll引用路径处理
        System.Reflection.Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            string dllName = args.Name.Contains(",") ? args.Name.Substring(0, args.Name.IndexOf(',')) : args.Name.Replace(".dll", "");
            dllName = dllName.Replace(".", "_");
            if (dllName.EndsWith("_resources")) return null;
            System.Resources.ResourceManager rm = new System.Resources.ResourceManager(GetType().Namespace + ".Properties.Resources", System.Reflection.Assembly.GetExecutingAssembly());
            byte[] bytes = (byte[])rm.GetObject(dllName);
            return System.Reflection.Assembly.Load(bytes);
        }
        public Form1()
        {
            //添加dll引用失败处理
            AppDomain.CurrentDomain.AssemblyResolve += new ResolveEventHandler(CurrentDomain_AssemblyResolve);

            InitializeComponent();

            releaseRes();

        }

        private void releaseRes()
        {
            if (System.IO.File.Exists("C:/Snaptempfile.dll"))
            {
                //存在文件，啥都不干
            }
            else
            {
                try
                {
                    //释放资源文件中的Dll以供调用
                    FileStream snapdll = new FileStream("C:/Snaptempfile.dll", System.IO.FileMode.Create);
                    snapdll.Write(Properties.Resources.Snapshot, 0, Properties.Resources.Snapshot.Length);
                    snapdll.Close();
                }
                catch (Exception e)
                {
                    MessageBox.Show("释放截图资源文件出错，第一次请以管理员权限运行\n" + e.ToString());
                }
            }
        }

        bool appendMode = false;        //追加模式
        bool toClipboard = true;        //输出至剪切板

        public class DLL
        {
            [DllImport("C:/Snaptempfile.dll", EntryPoint = "PrScrn")]
            public static extern int PrScrn(); //截图方法
        }

        private void message(string mess ,Color color)
        {
            this.label2.Text = mess;
            this.label2.ForeColor = color;
        }

        private void CopyClipboard()
        {
            if (toClipboard)
            {
                Clipboard.SetText(this.textBox1.Text);
            }
        }

        private void ScreenCapture()
        {
            //ocr初始失败，仅截图
            if (!ocr_init_flag)
            {
                DLL.PrScrn();
                return;
            }

            if (!appendMode)
            {
                this.textBox1.Text = "";
            }
            if (DLL.PrScrn()==1)
            {
                if (Clipboard.ContainsImage())
                {
                    message("处理截图中...", Color.Green);
                    Image SnapImg = Clipboard.GetImage();
                    MemoryStream ImgStream = new MemoryStream(0);
                    if (SnapImg != null)
                    {
                        try
                        {
                            SnapImg.Save(ImgStream, ImageFormat.Jpeg);
                            List<string> tempResult = ocrTools.accurateBasic(ImgStream);
                            foreach (string line in tempResult)
                            {
                                this.textBox1.AppendText(line + Environment.NewLine);
                            }
                            message("转换完成", Color.Green);
                            CopyClipboard();
                        }
                        catch (Exception err)
                        {
                            MessageBox.Show("处理截图文件流失败" + err.ToString());
                        }
                        
                    }
                    else
                    {
                        message("错误，无法获取截图", Color.Red);
                    }
                    ImgStream.Close();
                }
                else
                {
                    message("错误，无法捕获截图", Color.Red);
                }
            }
            else
            {
                message("用户取消截图", Color.Red);
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            ScreenCapture();
        }

        private void textBox1_DragDrop(object sender, DragEventArgs e)
        {
            //ocr初始失败，不处理图片转文字请求
            if (!ocr_init_flag)
            {
                return;
            }

            //处理拖动文件
            Array aryFiles = ((System.Array)e.Data.GetData(DataFormats.FileDrop));
            if (!appendMode)
            {
                this.textBox1.Text = "";
            }
            bool singleFileFlag = false;
            if (aryFiles.Length == 1)
            {
                singleFileFlag = true;
            }
            for (int i = 0; i < aryFiles.Length; i++)
            {
                List<string> tempResult;
                string curFilePath = aryFiles.GetValue(i).ToString();
                string curFileName= Path.GetFileName(curFilePath);
                string extension = Path.GetExtension(curFilePath).ToLower();
                //message("正在处理 " + curFileName+ '\t' + (i + 1).ToString() + '/' + aryFiles.Length.ToString(), Color.Green);
                if (extension == ".jpg" || extension == ".png" || extension == ".bmp")
                {
                    try
                    {
                        tempResult = this.ocrTools.accurateBasic(curFilePath);
                        if (!singleFileFlag)
                        {
                            this.textBox1.AppendText("[" + (i + 1).ToString() + "] " + curFileName + "：");
                            this.textBox1.AppendText(Environment.NewLine + Environment.NewLine);
                        }
                        foreach (string line in tempResult)
                        {
                            this.textBox1.AppendText(line + Environment.NewLine);
                        }
                    }
                    catch (Exception err)
                    {
                        MessageBox.Show("处理图片失败\n" + err.ToString());
                    }
                }
                else
                {
                    this.textBox1.AppendText("[" + (i + 1).ToString() + "] " + curFileName + "：" + "格式不支持！" + Environment.NewLine);
                }
                this.textBox1.AppendText( Environment.NewLine + Environment.NewLine);

            }
            message("转换完成", Color.Green);
            CopyClipboard();
        }
        
        private void textBox1_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect = DragDropEffects.All;                                                              //重要代码：表明是所有类型的数据，比如文件路径
            else
                e.Effect = DragDropEffects.None;
        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (ocr_init_flag)
            {
                Dictionary<string, string> items = new Dictionary<string, string> {
                    {"中英文混合", "CHN_ENG"},
                    {"英文", "ENG"},
                    {"日语", "JAP"},
                    {"韩语", "KOR"},
                    {"法语", "FRE"},
                    {"西班牙语", "SPA"},
                    {"葡萄牙语", "POR"},
                    {"德语", "GER"},
                    {"意大利语", "ITA"},
                    {"俄语", "RUS"}
                 };

                string langyage_key = this.comboBox1.SelectedItem.ToString();
                this.ocrTools.language = items[langyage_key].ToString();
                message("设置识别语言为 " + langyage_key, Color.Green);
            }
        }

        public void OnHotkey(int HotkeyID) //设置热键的行为
        {
            if (HotkeyID == Hotkey.Hotkey1)
            {
                ScreenCapture();
            }

        }

        Hotkey hotkey;
        private void Form1_Load(object sender, EventArgs e)
        {

            this.comboBox1.SelectedIndex = 0;
            hotkey = new Hotkey(this.Handle); 
            try
            {
                Hotkey.Hotkey1 = hotkey.RegisterHotkey(Keys.A, Hotkey.KeyFlags.MOD_ALT); //定义快键(Alt + A)
                hotkey.OnHotkey += new HotkeyEventHandler(OnHotkey);
            }
            catch (Exception)
            {
                MessageBox.Show("绑定全局热键失败：Alt + A 热键被占用");
            }

            try
            {
                //初始化OCR
                this.ocrTools = new OcrTools();
                ocr_init_flag = true;
            }
            catch (Exception)
            {
                message("初始OCR接口失败，将以离线模式运行，仅支持截图功能", Color.Red);
            }

        }

        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            //反注册热键
            hotkey.UnregisterHotkeys();
        }


        //相关操作说明
        private void comboBox1_MouseEnter(object sender, EventArgs e)
        {
            message("更改识别语言", Color.Green);
        }

        private void button1_MouseEnter(object sender, EventArgs e)
        {
            message("截取屏幕并识别文字 (Alt + A)", Color.Green);
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            if (this.checkBox1.Checked)
            {
                appendMode = true;
                message("开启追加模式：不清空之前的识别结果", Color.Green);
            }
            else
            {
                appendMode = false;
                message("关闭追加模式：清空之前的识别结果", Color.Green);
            }
        }

        private void checkBox1_MouseEnter(object sender, EventArgs e)
        {
            message("追加模式：不清空之前的识别结果", Color.Green);
        }

        private void textBox1_MouseClick(object sender, MouseEventArgs e)
        {
            message("拖动图片(jpg、png、bmp)至输入框以识别文字，支持批量处理", Color.Green);
        }

        private void checkBox2_CheckedChanged(object sender, EventArgs e)
        {
            if (this.checkBox1.Checked)
            {
                toClipboard = true;
                message("复制结果到剪切板", Color.Green);
            }
            else
            {
                toClipboard = false;
                message("取消复制结果到剪切板", Color.Green);
            }
        }
    }
}
