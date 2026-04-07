using System;
using System;
using System.Collections.Generic;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO.Ports;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WinformPractise
{
    public partial class Form1 : Form
    {
        SerialPort serialPort = new SerialPort();
        public Form1()
        {
            InitializeComponent();

        }
        private void cboPortName_SelectedIndexChanged(object sender, EventArgs e)
        {
            string Name = cboPortName.Text;
            AddLog($"将串口更改为{Name}");
        }

        private void cboBaudRate_SelectedIndexChanged(object sender, EventArgs e)
        {
            string BaudRate = cboBaudRate.Text;
            AddLog($"将波特率改为{BaudRate}");
        }
        private void InitModbusTable() 
        {
           dgvModbus.Columns.Clear();//自动清除列防止程序多次启动不断增加列导致报错
           dgvModbus.Rows.Clear();//清除行

            //手动创建列
            dgvModbus.Columns.Add("Addr","寄存器地址");
            dgvModbus.Columns.Add("Name","寄存器名称");
            dgvModbus.Columns.Add("Value", "数据值");
            dgvModbus.Columns.Add("Status", "设备状态");
            //手动创建行
            dgvModbus.Rows.Add("0x00", "温度值", "--", "正常");
            dgvModbus.Rows.Add("0x01", "湿度值", "--", "正常");
            dgvModbus.Rows.Add("0x02", "运行状态", "--", "Run");

            dgvModbus.AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill;//自动设置最佳列宽，防止“黑框”剩余
            dgvModbus.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;//行宽不作处理(无Fill方法)
            dgvModbus.BackgroundColor = this.BackColor;//让背景色跟窗体一致

            // 动态计算行高
            if (dgvModbus.Rows.Count > 0)
            {
                // 表格总高度 - 表头高度 = 所有行可分配的总高度
                int available_height = dgvModbus.Height - dgvModbus.ColumnHeadersHeight;
                // 平均分配给每一行
                int row_height = available_height / dgvModbus.Rows.Count;

                // 立即设置现有行的高度
                foreach (DataGridViewRow row in dgvModbus.Rows)
                {
                    row.Height = row_height;
                }
                // 后续新增行也会自动使用这个高度
                dgvModbus.RowTemplate.Height = row_height;
            }
            

        }
        private byte[] BuildModbusReadCommand()
        {
            byte[] command = new byte[6];
            command[0] = 0x01;
            command[1] = 0x03;
            command[2] = 0x00;
            command[3] = 0x00;
            command[4] = 0x00;
            command[5] = 0x02;

            byte[] crc = CalculateCRC16(command);
            byte[] fullCommand = new byte[8];
            Array.Copy(command, fullCommand, 6);
            fullCommand[6] = crc[0];
            fullCommand[7] = crc[1];

            return fullCommand;
        }

        private void UpdateModbusTable(double temp, double humi,string status,Color color) 
        {
            if (dgvModbus.Rows.Count < 3) return;
            dgvModbus.Rows[0].Cells["Value"].Value = temp.ToString("0.0");
            dgvModbus.Rows[1].Cells["Value"].Value = humi.ToString("0.0");
            dgvModbus.Rows[2].Cells["Value"].Value = status;
            dgvModbus.Rows[2].DefaultCellStyle.BackColor = color;
            dgvModbus.Rows[2].DefaultCellStyle.ForeColor = Color.White;
        }

        private void ClearModbusTable()
        {
            if (dgvModbus.Rows.Count < 3) return;
            // 恢复温度、湿度为--
            dgvModbus.Rows[0].Cells["Value"].Value = "--";
            dgvModbus.Rows[1].Cells["Value"].Value = "--";
            // 恢复运行状态默认值
            dgvModbus.Rows[2].Cells["Value"].Value = "Run";
            dgvModbus.Rows[2].DefaultCellStyle.BackColor = this.BackColor;
            dgvModbus.Rows[2].DefaultCellStyle.ForeColor = Color.Black;
        }

        //手动计算CRC16校验 
        private byte[] CalculateCRC16(byte[] data) 
        {
          ushort crc = 0xFFFF;
            for (int i = 0; i < data.Length; i++) //遍历需要校验的字节
            {
             crc ^= data[i];//异或运算
                for (int j = 0; j < 8; j++)//8次位移运算 
                {
                    if ((crc & 1) > 0)//判断最后一位是否为1
                    {   //右移一位再异或多项式
                        crc >>= 1;
                        crc ^= 0xA001;

                    }
                    else 
                    {
                     crc>>= 1;//为0只右移一位
                    }                           
                }        
            }

            return new byte[] { (byte)crc, (byte)(crc >> 8) };//校验码低八位在前，高八位在后
        }
        private void AddLog(string msg) //使用日志
        {
            string time = DateTime.Now.ToString("yyyy/MM/dd:HH:mm:ss");
            lstLog.Items.Add($"{time}{msg}");
            lstLog.TopIndex = lstLog.Items.Count - 1;
        }

        
        //窗体加载自启动
        private void Form1_Load(object sender, EventArgs e)
        {
            string[] ports = SerialPort.GetPortNames();
            cboPortName.Items.AddRange(ports);
            cboBaudRate.Items.AddRange(new string[] { "9600", "19200", "38400" });
            cboBaudRate.Text = "9600";

            btnCloseSerial.Enabled = false;
            btnSendData.Enabled = false;
            btnOpenSerial.Enabled = true;
            

            InitModbusTable();
        }

        private void btnOpenSerial_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(cboPortName.Text))
            {
                AddLog("【提示】请选择串口号");
                return;
            }
            if (serialPort.IsOpen)
            {
                AddLog($"{serialPort.PortName}【提示】串口已打开，请勿重复操作");
                return;
            }
            try
            {
                int baudRate = Convert.ToInt32(cboBaudRate.Text);//字符串转为整数
                string comName = cboPortName.Text;
                //串口配置
                serialPort.BaudRate = baudRate;
                serialPort.PortName = comName;
                serialPort.DataBits = 8;
                serialPort.Parity = Parity.None;
                serialPort.StopBits = StopBits.One;

                serialPort.PortName = cboPortName.Text;
                serialPort.Open();
                AddLog($"【成功】{serialPort.PortName}串口已打开");
                timerReceive.Start();

                cboPortName.Enabled = false;
                cboBaudRate.Enabled = false;
                btnCloseSerial.Enabled = true;
                btnOpenSerial.Enabled = false;
                btnSendData.Enabled = true;
            }
            catch (Exception ex)
            {
                AddLog($"【错误】串口打开失败{ex.Message}");
            }
        }

        private void btnCloseSerial_Click(object sender, EventArgs e)
        {
            try
            {
                if (serialPort.IsOpen)
                {
                    serialPort.Close();
                    AddLog($"【成功】{serialPort.PortName}串口已关闭");
                    timerReceive.Stop();
                }
                else
                {
                    AddLog($"【提示】{serialPort.PortName}串口已关闭，请勿重复操作！");
                }
            }
            catch (Exception ex)
            {
                AddLog($"【错误】{serialPort.PortName}串口关闭失败！{ex.Message}");
            }
            finally
            {
                cboPortName.Enabled = true;
                cboBaudRate.Enabled= true;
                btnCloseSerial.Enabled = false;
                btnOpenSerial.Enabled = true;
                btnSendData.Enabled = false;
            }
        }

        private void btnSendData_Click(object sender, EventArgs e)
        {
            if (!serialPort.IsOpen)
            {
                AddLog("【提示】请先打开串口");
                return;
            }

            try
            {
                byte[] modbusCmd = BuildModbusReadCommand();
                serialPort.Write(modbusCmd, 0, modbusCmd.Length);//0：从报文的第一个字节开始发发到最后一个modbusLength
                AddLog($"{serialPort.PortName}发送报文" + BitConverter.ToString(modbusCmd).Replace("-", " "));
            }
            catch (Exception ex)
            {
                AddLog($"【错误】发送失败{ex.Message}");
            }
        }

        private void timerReceive_Tick(object sender, EventArgs e)
        {
            if (!serialPort.IsOpen) return;

            try
            {
                if (serialPort.BytesToRead <= 0) return;// 1只有缓冲区有数据才处理

                byte[] allData = new byte[serialPort.BytesToRead]; //  一次性读取所有缓冲区数据，避免多次读取导致的粘包
                serialPort.Read(allData, 0, allData.Length);

                int index = 0;
                while (index <= allData.Length - 9)// 循环查找完整的9字节Modbus报文（精准匹配报文头01 03）
                {
                    if (allData[index] == 0x01 && allData[index + 1] == 0x03)// 找到Modbus标准报文头：从站地址01 + 功能码03
                    {
                        byte[] buffer = new byte[9];// 提取完整的9字节有效报文
                        Array.Copy(allData, index, buffer, 0, 9);

                        AddLog($"{serialPort.PortName}接收报文：" + BitConverter.ToString(buffer).Replace("-", ""));

                        byte[] data = new byte[7];
                        Array.Copy(buffer, 0, data, 0, 7);

                        byte[] calcCrc = CalculateCRC16(data);
                        byte recvCrcLow = buffer[7];
                        byte recvCrcHigh = buffer[8];

                        AddLog($"计算CRC：{calcCrc[0]:X2} {calcCrc[1]:X2} | 接收CRC：{recvCrcLow:X2} {recvCrcHigh:X2}");// 打印CRC对比，彻底透明化

                        // CRC校验
                        if (calcCrc[0] == recvCrcLow && calcCrc[1] == recvCrcHigh)
                        {
                            // 校验通过，解析数据
                            if (buffer[2] == 0x04)
                            {
                                short tempRaw = (short)(buffer[3] << 8 | buffer[4]);
                                double temperature = tempRaw / 10.0;

                                string status;
                                Color color;
                                if (temperature > 35)
                                {
                                    status = "高温预警";
                                    color = Color.Red;
                                }
                                else if (temperature < 0)
                                {
                                    status = "低温异常";
                                    color = Color.Orange;
                                }
                                else
                                {
                                    status = "正常";
                                    color = Color.Green;
                                }

                                ushort humiRaw = (ushort)(buffer[5] << 8 | buffer[6]);
                                double humidity = humiRaw / 10.0;

                                // 更新界面表格
                                UpdateModbusTable(temperature, humidity, status, color);
                                AddLog("【校验成功】数据解析完成！");
                            }
                        }
                        else
                        {
                            AddLog("【CRC校验失败】报文损坏，已丢弃！");
                            ClearModbusTable(); 
                        }
                        index += 9;// 处理完所有数据，清空缓冲区，避免残留数据影响下一次接收
                    }
                    else
                    {
                        index++; // 不是报文头，往后偏移1位继续找
                    }
                }
                serialPort.DiscardInBuffer();// 处理完所有数据，清空缓冲区，避免残留数据影响下一次接收
            }
            catch (Exception ex)
            {
                AddLog($"【错误】{ex.Message}");
                ClearModbusTable();
                serialPort.DiscardInBuffer();
            }
        }

        private void btnClearLog_Click(object sender, EventArgs e)
        {
            lstLog.Items.Clear();
            AddLog("操作日志已清除");
        }
    }
}