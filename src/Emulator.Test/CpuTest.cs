﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using Fs6502.Assembler;

namespace Fs6502.Emulator.Test
{
    [TestFixture]
    class CpuTest
    {
        [TestCase]
        public void Addressing_ZeroPage()
        {
            var program = new Assembler.Assembler().Assemble(new String[]
                {
                    "LDA $2F"
                });

            var cpu = new Cpu();
            cpu.Status.Memory[new Emulator.Word(0x00, 0x2F)] = 0x05;

            cpu.Execute(0, program.ToArray());

            Assert.AreEqual(0x05, cpu.Status.Accumulator);
            Assert.AreEqual(3, cpu.Status.Cycles);
            Assert.AreEqual(new Word(2), cpu.Status.ProgramCounter);
        }

        [TestCase]
        public void Addressing_ZeroPageX()
        {
            var program = new Assembler.Assembler().Assemble(new String[]
                {
                    "LDX #$08", //2 cycles
                    "LDA $27,X" //4 cycles
                });

            var cpu = new Cpu();
            cpu.Status.Memory[new Emulator.Word(0x00, 0x2F)] = 0x05;

            cpu.Execute(0, program.ToArray());

            Assert.AreEqual(0x05, cpu.Status.Accumulator);
            Assert.AreEqual(6, cpu.Status.Cycles);
            Assert.AreEqual(new Word(4).ToString(), cpu.Status.ProgramCounter.ToString());
        }

        [TestCase]
        public void Addressing_ZeroPageX_Wrapping()
        {
            var program = new Assembler.Assembler().Assemble(new String[]
                {
                    "LDX #$08", //2 cycles
                    "LDA $FB,X" //4 cycles
                });

            var cpu = new Cpu();
            cpu.Status.Memory[new Emulator.Word(0x00, 0x03)] = 0x04;

            cpu.Execute(0, program.ToArray());

            Assert.AreEqual(0x04, cpu.Status.Accumulator);
            Assert.AreEqual(6, cpu.Status.Cycles);
            Assert.AreEqual(new Word(4).ToString(), cpu.Status.ProgramCounter.ToString());
        }

        [TestCase]
        public void Addressing_ZeroPageY()
        {
            var program = new Assembler.Assembler().Assemble(new String[]
                {
                    "LDY #$05", //2 cycles
                    "LDX $16,Y" //4 cycles
                });

            var cpu = new Cpu();
            cpu.Status.Memory[new Emulator.Word(0x00, 0x1B)] = 0x07;

            cpu.Execute(0, program.ToArray());

            Assert.AreEqual(0x07, cpu.Status.X);
            Assert.AreEqual(6, cpu.Status.Cycles);
            Assert.AreEqual(new Word(4).ToString(), cpu.Status.ProgramCounter.ToString());
        }

        [TestCase]
        public void Addressing_Absolute()
        {
            var program = new Assembler.Assembler().Assemble(new String[]
                {
                    "LDA $0155" //4 cycles
                });

            var cpu = new Cpu();
            cpu.Status.Memory[new Emulator.Word(0x01, 0x55)] = 0x12;

            cpu.Execute(0, program.ToArray());

            Assert.AreEqual(0x12, cpu.Status.Accumulator);
            Assert.AreEqual(4, cpu.Status.Cycles);
            Assert.AreEqual(new Word(3).ToString(), cpu.Status.ProgramCounter.ToString());
        }

        [TestCase]
        public void Addressing_AbsoluteX()
        {
            var program = new Assembler.Assembler().Assemble(new String[]
                {
                    "LDX #$15",    //2 cycles
                    "LDA $0360,X"  //4 cycles
                });

            var cpu = new Cpu();
            cpu.Status.Memory[new Emulator.Word(0x03, 0x75)] = 0x16;

            cpu.Execute(0, program.ToArray());

            Assert.AreEqual(0x16, cpu.Status.Accumulator);
            Assert.AreEqual(6, cpu.Status.Cycles);
            Assert.AreEqual(new Word(5).ToString(), cpu.Status.ProgramCounter.ToString());
        }

        [TestCase]
        public void Addressing_AbsoluteX_PageCrossed()
        {
            var program = new Assembler.Assembler().Assemble(new String[]
                {
                    "LDX #$AA",    //2 cycles
                    "LDA $0789,X"  //5 cycles
                });

            var cpu = new Cpu();
            cpu.Status.Memory[new Emulator.Word(0x08, 0x33)] = 0x20;

            cpu.Execute(0, program.ToArray());

            Assert.AreEqual(0x20, cpu.Status.Accumulator);
            Assert.AreEqual(7, cpu.Status.Cycles);
            Assert.AreEqual(new Word(5).ToString(), cpu.Status.ProgramCounter.ToString());
        }

        [TestCase]
        public void Addressing_AbsoluteY()
        {
            var program = new Assembler.Assembler().Assemble(new String[]
                {
                    "LDY #$2A",    //2 cycles
                    "LDA $2104,Y"  //4 cycles
                });

            var cpu = new Cpu();
            cpu.Status.Memory[new Emulator.Word(0x21, 0x2E)] = 0x11;

            cpu.Execute(0, program.ToArray());

            Assert.AreEqual(0x11, cpu.Status.Accumulator);
            Assert.AreEqual(6, cpu.Status.Cycles);
            Assert.AreEqual(new Word(5).ToString(), cpu.Status.ProgramCounter.ToString());
        }

        [TestCase]
        public void Addressing_AbsoluteY_PageCrossed()
        {
            var program = new Assembler.Assembler().Assemble(new String[]
                {
                    "LDY #$FE",    //2 cycles
                    "LDA $1652,Y"  //5 cycles
                });

            var cpu = new Cpu();
            cpu.Status.Memory[new Emulator.Word(0x17, 0x50)] = 0x04;

            cpu.Execute(0, program.ToArray());

            Assert.AreEqual(0x04, cpu.Status.Accumulator);
            Assert.AreEqual(7, cpu.Status.Cycles);
            Assert.AreEqual(new Word(5).ToString(), cpu.Status.ProgramCounter.ToString());
        }

        [TestCase]
        public void Addressing_IndirectX()
        {
            var program = new Assembler.Assembler().Assemble(new String[]
                {
                    "LDX #$12",    //2 cycles
                    "LDA ($52,X)"  //6 cycles
                });

            var cpu = new Cpu();
            cpu.Status.Memory[new Emulator.Word(0x00, 0x64)] = 0x45;
            cpu.Status.Memory[new Emulator.Word(0x00, 0x65)] = 0xB2;
            cpu.Status.Memory[new Emulator.Word(0xB2, 0x45)] = 0x19;

            cpu.Execute(0, program.ToArray());

            Assert.AreEqual(0x19, cpu.Status.Accumulator);
            Assert.AreEqual(8, cpu.Status.Cycles);
            Assert.AreEqual(new Word(4).ToString(), cpu.Status.ProgramCounter.ToString());
        }

        [TestCase]
        public void Addressing_IndirectX_Wrapping()
        {
            var program = new Assembler.Assembler().Assemble(new String[]
                {
                    "LDX #$A5",    //2 cycles
                    "LDA ($D2,X)"  //6 cycles
                });

            var cpu = new Cpu();
            cpu.Status.Memory[new Emulator.Word(0x00, 0x77)] = 0x45;
            cpu.Status.Memory[new Emulator.Word(0x00, 0x78)] = 0xB2;
            cpu.Status.Memory[new Emulator.Word(0xB2, 0x45)] = 0x19;

            cpu.Execute(0, program.ToArray());

            Assert.AreEqual(0x19, cpu.Status.Accumulator);
            Assert.AreEqual(8, cpu.Status.Cycles);
            Assert.AreEqual(new Word(4).ToString(), cpu.Status.ProgramCounter.ToString());
        }

        [TestCase]
        public void Addressing_IndirectY()
        {
            var program = new Assembler.Assembler().Assemble(new String[]
                {
                    "LDY #$3",     //2 cycles
                    "LDA ($52),Y"  //5 cycles
                });

            var cpu = new Cpu();
            cpu.Status.Memory[new Emulator.Word(0x00, 0x52)] = 0xF5;
            cpu.Status.Memory[new Emulator.Word(0x00, 0x53)] = 0x1A;
            cpu.Status.Memory[new Emulator.Word(0x1A, 0xF8)] = 0x21;

            cpu.Execute(0, program.ToArray());

            Assert.AreEqual(0x21, cpu.Status.Accumulator);
            Assert.AreEqual(7, cpu.Status.Cycles);
            Assert.AreEqual(new Word(4).ToString(), cpu.Status.ProgramCounter.ToString());
        }

        [TestCase]
        public void Addressing_IndirectY_PageCrossed()
        {
            var program = new Assembler.Assembler().Assemble(new String[]
                {
                    "LDY #$A5",    //2 cycles
                    "LDA ($59),Y"  //6 cycles
                });

            var cpu = new Cpu();
            cpu.Status.Memory[new Emulator.Word(0x00, 0x59)] = 0xF3;
            cpu.Status.Memory[new Emulator.Word(0x00, 0x5A)] = 0x2B;
            cpu.Status.Memory[new Emulator.Word(0x2C, 0x98)] = 0x55;

            cpu.Execute(0, program.ToArray());

            Assert.AreEqual(0x55, cpu.Status.Accumulator);
            Assert.AreEqual(8, cpu.Status.Cycles);
            Assert.AreEqual(new Word(4).ToString(), cpu.Status.ProgramCounter.ToString());
        }

        [TestCaseSource("TestData")]
        public void Execute(CpuTestData programData)
        {
            var cpu = new Cpu();
            cpu.Execute(programData.EntryPoint, programData.MachineCode);

            Assert.IsTrue(programData.Assertion(cpu.Status));
        }

        public class CpuTestData
        {
            public ushort EntryPoint { get; set; }
            public byte[] MachineCode { get; set; }
            public Func<Status, bool> Assertion { get; set; }

            public CpuTestData(ushort entryPoint, byte[] machineCode, Func<Status, bool> assertion)
            {
                this.EntryPoint = entryPoint;
                this.MachineCode = machineCode;
                this.Assertion = assertion;
            }
        }


        protected static CpuTestData[] TestData =
        {
            //Following code courtesy of Nick Morgan, https://skilldrick.github.io/easy6502/
            new  CpuTestData(
                1536,
                new Assembler.Assembler().Assemble(new String[]
                {
                    "  LDA #$01",     //2 cycles
                    "  STA $0200",    //4 cycles
                    "  LDA #$05",     //2 cycles
                    "  STA $0201",    //4 cycles
                    "  LDA #$08",     //2 cycles
                    "  STA $0202"     //4 cycles
                }).ToArray(),
                status => (status.Accumulator == 0x08) && (status.ProgramCounter.ToInt32 == 1551) && (status.Cycles == 18)),
            new  CpuTestData(
                1536,
                new Assembler.Assembler().Assemble(new String[]
                {
                    "  LDX #$08",     //2 cycles
                    "decrement:",
                    "  DEX",          //2 cycles
                    "  STX $0200",    //4 cycles
                    "  CPX #$03",     //2 cycles
                    "  BNE decrement",//branch taken 4 times: 14
                    "  STX $0201",    //4 cycles
                    "  BRK"           //7 cycles
                }).ToArray(),
                status => (status.X == 0x03) && (status.ProgramCounter.ToInt32 == 1550) && status.Flags.Carry && status.Flags.Zero && (status.Cycles == 67)),
            new  CpuTestData(
                1536,
                new Assembler.Assembler().Assemble(new String[]
                {
                    "  LDX #$00",       //2 cycles
                    "  LDY #$00",       //2 cycles
                    "firstloop:",
                    "  TXA",            //2 cycles
                    "  STA $0200,Y",    //5 cycles
                    "  PHA",            //3 cycles
                    "  INX",            //2 cycles
                    "  INY",            //2 cycles
                    "  CPY #$10",       //2 cycles
                    "  BNE firstloop ;loop until Y is $10", //branch taken 15 times: 47
                    "secondloop:",
                    "  PLA",            //4 cycles
                    "  STA $0200,Y",    //5 cycles
                    "  INY",            //2 cycles
                    "  CPY #$20      ;loop until Y is $20", //2 cycles
                    "  BNE secondloop"  //branch taken 15 times: 47
                }).ToArray(),
                status => (status.X == 0x10) && (status.Y == 0x20) && (status.ProgramCounter.ToInt32 == 1560) && status.Flags.Carry && status.Flags.Zero && (status.Cycles == 562)),
        };
    }
}
