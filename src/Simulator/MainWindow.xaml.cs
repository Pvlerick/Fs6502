using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
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

            var buffer = Array.CreateInstance(typeof(byte), 32 * 32) as byte[];
            buffer[0] = 0xA1;
            buffer[1] = 0xA2;
            buffer[2] = 0x03;
            buffer[3] = 0xD4;
            buffer[4] = 0xD5;
            buffer[5] = 0x06;
            buffer[32] = 0x10;
            buffer[33] = 0x11;
            buffer[1000] = 0x07;
            buffer[1023] = 0x08;

            this.DrawDisplay(buffer);
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
    }
}
