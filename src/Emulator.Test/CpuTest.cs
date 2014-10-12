using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Emulator.Test
{
    [TestFixture]
    class CpuTest
    {
        [TestCaseSource("TestData")]
        public void Execute(CpuTestData programData)
        {
            var cpu = new Cpu();
            cpu.Execute(programData.StartingAddress, programData.MachineCode);

            Assert.IsTrue(programData.Assertion(cpu.Status));
        }

        public class CpuTestData
        {
            public ushort StartingAddress { get; set; }
            public byte[] MachineCode { get; set; }
            public Func<Status, bool> Assertion { get; set; }

            public CpuTestData(ushort startingAddress, byte[] machineCode, Func<Status, bool> assertion)
            {
                this.StartingAddress = startingAddress;
                this.MachineCode = machineCode;
                this.Assertion = assertion;
            }
        }


        protected static CpuTestData[] TestData =
        {
            //Following code courtesy of Nick Morgan, https://skilldrick.github.io/easy6502/
            new  CpuTestData(
                1536,
                new byte[] { 0xa9, 0x01, 0x8d, 0x00, 0x02, 0xa9, 0x05, 0x8d, 0x01, 0x02, 0xa9, 0x08, 0x8d, 0x02, 0x02 },
                status => (status.Registers.Accumulator == 0x08) && (status.Registers.ProgramCounter == 1551)),
            new  CpuTestData(
                1536,
                new byte[] { 0xa2, 0x08, 0xca, 0x8e, 0x00, 0x02, 0xe0, 0x03, 0xd0, 0xf8, 0x8e, 0x01, 0x02, 0x00 },
                status => (status.Registers.X == 0x03) && (status.Registers.ProgramCounter == 1550) && status.Registers.Flags.Carry && status.Registers.Flags.Zero),
            new  CpuTestData(
                1536,
                new byte[] { 0xa2, 0x00, 0xa0, 0x00, 0x8a, 0x99, 0x00, 0x02, 0x48, 0xe8, 0xc8, 0xc0, 0x10, 0xd0, 0xf5, 0x68, 0x99, 0x00, 0x02, 0xc8, 0xc0, 0x20, 0xd0, 0xf7 },
                status => (status.Registers.X == 0x10) && (status.Registers.Y == 0x20) && (status.Registers.ProgramCounter == 1560) && status.Registers.Flags.Carry && status.Registers.Flags.Zero),
        };
    }
}
