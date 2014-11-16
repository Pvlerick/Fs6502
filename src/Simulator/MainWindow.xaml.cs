using Fs6502.Assembler;
using Fs6502.Emulator;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Simulator
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            this.Assembler = new Assembler();
            this.Cpu = new Cpu();

            this.CodeLines.Text = @"  LDX #$00
  LDY #$00
firstloop:
  TXA
  STA $0200,Y
  PHA
  INX
  INY
  CPY #$10
  BNE firstloop ;loop until Y is $10
secondloop:
  PLA
  STA $0200,Y
  INY
  CPY #$20      ;loop until Y is $20
  BNE secondloop";
        }

        private Assembler Assembler { get; set; }
        private Cpu Cpu { get; set; }

        private void Run(object sender, RoutedEventArgs e)
        {
            this.RunButton.IsEnabled = false;

            var machineCode = this.Assembler.Assemble(this.CodeLines.Text.Split('\n'));
            this.Cpu.Execute(1536, machineCode.ToArray());

            Task.Factory.StartNew(() =>
            {
                while (true)
                {
                    this.Dispatcher.Invoke(() =>
                    {
                        this.RefreshDisplay();
                        this.RefreshCpuStatusDisplay();
                    });
                    Thread.Sleep(250);
                }
            });

            this.RunButton.IsEnabled = true;
        }

        private void RefreshDisplay()
        {
            //This simulator uses the memory locations $0200 to $05ff, similat to https://github.com/skilldrick/6502js
            var buffer = this.GetMemorySlice(new Word(512), 1024);

            this.DrawDisplay(buffer.ToArray());
        }

        private IEnumerable<byte> GetMemorySlice(Word startAddress, int size)
        {
            for (ushort i = 0; i < size; i++)
            {
                yield return this.Cpu.Status.Memory[startAddress.Add(i)];
            }
        }

        const int PixelMultiplier = 5;
        const int BufferBorderSize = 32;
        const int DisplayBorderSize = BufferBorderSize * PixelMultiplier;

        private void DrawDisplay(byte[] buffer)
        {
            var dis = new byte[DisplayBorderSize * DisplayBorderSize];

            for (int i = 0; i < Math.Pow(BufferBorderSize, 2); i++)
            {
                var startingPoint = (i / 32) * ((DisplayBorderSize * DisplayBorderSize) / BufferBorderSize);

                for (int vertical = 0; vertical < PixelMultiplier; vertical++)
                {
                    var voffset = startingPoint + (vertical * DisplayBorderSize) + (i % 32) * 5;

                    for (int horizontal = 0; horizontal < PixelMultiplier; horizontal++)
                    {
                        dis[voffset + horizontal] = buffer[i];
                    }
                }
            }

            //Go check http://stackoverflow.com/questions/1983781/why-does-bitmapsource-create-throw-an-argumentexception/1983886#1983886
            var stride = ((DisplayBorderSize * 8 + 7) & ~7) / 8;

            this.display.Source = BitmapSource.Create(DisplayBorderSize, DisplayBorderSize, 96, 96, PixelFormats.Indexed8, BitmapPalettes.WebPalette, dis, stride);
        }

        private void RefreshCpuStatusDisplay()
        {
            this.StatusAccumulator.Text = this.Cpu.Status.Accumulator.ToString("X2");
            this.StatusX.Text = this.Cpu.Status.X.ToString("X2");
            this.StatusY.Text = this.Cpu.Status.Y.ToString("X2");
            this.StatusStackPointer.Text = this.Cpu.Status.StackPointer.ToString("X2");
            this.StatusProgramCounter.Text = this.Cpu.Status.ProgramCounter.ToString();
        }
    }
}
