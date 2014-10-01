namespace Fs6502

    open System.Text.RegularExpressions

    type Cpu() =
        member this.Registers = new Registers()
        member this.Flags = new Flags()
        member this.Memory = new Memory()

        //Others
        member val Cycles = 0ul with get, set

        //Parse a line and map it to an instruction
        //See Opcodes here: http://www.6502.org/tutorials/6502opcodes.html
        member this.Execute instruction =
            //Define all opcodes for pattern matching use
            let (|ADC|_|) (instruction: string) = if instruction.StartsWith "ADC" then Some(this.ParseOperand(instruction.Substring(4))) else None
            let (|AND|_|) (instruction: string) = if instruction.StartsWith "AND" then Some(this.ParseOperand(instruction.Substring(4))) else None
            let (|ASL|_|) (instruction: string) = if instruction.StartsWith "ASL" then Some(this.ParseOperand(instruction.Substring(4))) else None
            let (|BCC|_|) (instruction: string) = if instruction.StartsWith "BCC" then Some(this.ParseOperand(instruction.Substring(4))) else None
            let (|BCS|_|) (instruction: string) = if instruction.StartsWith "BCS" then Some(this.ParseOperand(instruction.Substring(4))) else None
            let (|BEQ|_|) (instruction: string) = if instruction.StartsWith "BEQ" then Some(this.ParseOperand(instruction.Substring(4))) else None
            let (|BIT|_|) (instruction: string) = if instruction.StartsWith "BIT" then Some(this.ParseOperand(instruction.Substring(4))) else None
            let (|BMI|_|) (instruction: string) = if instruction.StartsWith "BMI" then Some(this.ParseOperand(instruction.Substring(4))) else None
            let (|BNE|_|) (instruction: string) = if instruction.StartsWith "BNE" then Some(this.ParseOperand(instruction.Substring(4))) else None
            let (|BPL|_|) (instruction: string) = if instruction.StartsWith "BPL" then Some(this.ParseOperand(instruction.Substring(4))) else None
            let (|BRK|_|) (instruction: string) = if instruction.StartsWith "BRK" then Some(this.ParseOperand(instruction.Substring(4))) else None
            let (|BVC|_|) (instruction: string) = if instruction.StartsWith "BVC" then Some(this.ParseOperand(instruction.Substring(4))) else None
            let (|BVS|_|) (instruction: string) = if instruction.StartsWith "BVS" then Some(this.ParseOperand(instruction.Substring(4))) else None
            let (|CLC|_|) (instruction: string) = if instruction.StartsWith "CLC" then Some(this.ParseOperand(instruction.Substring(4))) else None
            let (|CLD|_|) (instruction: string) = if instruction.StartsWith "CLD" then Some(this.ParseOperand(instruction.Substring(4))) else None
            let (|CLI|_|) (instruction: string) = if instruction.StartsWith "CLI" then Some(this.ParseOperand(instruction.Substring(4))) else None
            let (|CLV|_|) (instruction: string) = if instruction.StartsWith "CLV" then Some(this.ParseOperand(instruction.Substring(4))) else None
            let (|CMP|_|) (instruction: string) = if instruction.StartsWith "CMP" then Some(this.ParseOperand(instruction.Substring(4))) else None
            let (|CPX|_|) (instruction: string) = if instruction.StartsWith "CPX" then Some(this.ParseOperand(instruction.Substring(4))) else None
            let (|CPY|_|) (instruction: string) = if instruction.StartsWith "CPY" then Some(this.ParseOperand(instruction.Substring(4))) else None
            let (|DEC|_|) (instruction: string) = if instruction.StartsWith "DEC" then Some(this.ParseOperand(instruction.Substring(4))) else None
            let (|DEX|_|) (instruction: string) = if instruction.StartsWith "DEX" then Some(this.ParseOperand(instruction.Substring(4))) else None
            let (|DEY|_|) (instruction: string) = if instruction.StartsWith "DEY" then Some(this.ParseOperand(instruction.Substring(4))) else None
            let (|EOR|_|) (instruction: string) = if instruction.StartsWith "EOR" then Some(this.ParseOperand(instruction.Substring(4))) else None
            let (|INC|_|) (instruction: string) = if instruction.StartsWith "INC" then Some(this.ParseOperand(instruction.Substring(4))) else None
            let (|INX|_|) (instruction: string) = if instruction.StartsWith "INX" then Some(this.ParseOperand(instruction.Substring(4))) else None
            let (|INY|_|) (instruction: string) = if instruction.StartsWith "INY" then Some(this.ParseOperand(instruction.Substring(4))) else None
            let (|JMP|_|) (instruction: string) = if instruction.StartsWith "JMP" then Some(this.ParseOperand(instruction.Substring(4))) else None
            let (|JSR|_|) (instruction: string) = if instruction.StartsWith "JSR" then Some(this.ParseOperand(instruction.Substring(4))) else None
            let (|LDA|_|) (instruction: string) = if instruction.StartsWith "LDA" then Some(this.ParseOperand(instruction.Substring(4))) else None
            let (|LDX|_|) (instruction: string) = if instruction.StartsWith "LDX" then Some(this.ParseOperand(instruction.Substring(4))) else None
            let (|LDY|_|) (instruction: string) = if instruction.StartsWith "LDY" then Some(this.ParseOperand(instruction.Substring(4))) else None
            let (|LSR|_|) (instruction: string) = if instruction.StartsWith "LSR" then Some(this.ParseOperand(instruction.Substring(4))) else None
            let (|NOP|_|) (instruction: string) = if instruction.StartsWith "NOP" then Some(this.ParseOperand(instruction.Substring(4))) else None
            let (|ORA|_|) (instruction: string) = if instruction.StartsWith "ORA" then Some(this.ParseOperand(instruction.Substring(4))) else None
            let (|PHA|_|) (instruction: string) = if instruction.StartsWith "PHA" then Some(this.ParseOperand(instruction.Substring(4))) else None
            let (|PHP|_|) (instruction: string) = if instruction.StartsWith "PHP" then Some(this.ParseOperand(instruction.Substring(4))) else None
            let (|PLA|_|) (instruction: string) = if instruction.StartsWith "PLA" then Some(this.ParseOperand(instruction.Substring(4))) else None
            let (|PLP|_|) (instruction: string) = if instruction.StartsWith "PLP" then Some(this.ParseOperand(instruction.Substring(4))) else None
            let (|ROL|_|) (instruction: string) = if instruction.StartsWith "ROL" then Some(this.ParseOperand(instruction.Substring(4))) else None
            let (|ROR|_|) (instruction: string) = if instruction.StartsWith "ROR" then Some(this.ParseOperand(instruction.Substring(4))) else None
            let (|RTI|_|) (instruction: string) = if instruction.StartsWith "RTI" then Some(this.ParseOperand(instruction.Substring(4))) else None
            let (|RTS|_|) (instruction: string) = if instruction.StartsWith "RTS" then Some(this.ParseOperand(instruction.Substring(4))) else None
            let (|SBC|_|) (instruction: string) = if instruction.StartsWith "SBC" then Some(this.ParseOperand(instruction.Substring(4))) else None
            let (|SEC|_|) (instruction: string) = if instruction.StartsWith "SEC" then Some(this.ParseOperand(instruction.Substring(4))) else None
            let (|SED|_|) (instruction: string) = if instruction.StartsWith "SED" then Some(this.ParseOperand(instruction.Substring(4))) else None
            let (|SEI|_|) (instruction: string) = if instruction.StartsWith "SEI" then Some(this.ParseOperand(instruction.Substring(4))) else None
            let (|STA|_|) (instruction: string) = if instruction.StartsWith "STA" then Some(this.ParseOperand(instruction.Substring(4))) else None
            let (|STX|_|) (instruction: string) = if instruction.StartsWith "STX" then Some(this.ParseOperand(instruction.Substring(4))) else None
            let (|STY|_|) (instruction: string) = if instruction.StartsWith "STY" then Some(this.ParseOperand(instruction.Substring(4))) else None
            let (|TAX|_|) (instruction: string) = if instruction.StartsWith "TAX" then Some(this.ParseOperand(instruction.Substring(4))) else None
            let (|TAY|_|) (instruction: string) = if instruction.StartsWith "TAY" then Some(this.ParseOperand(instruction.Substring(4))) else None
            let (|TSX|_|) (instruction: string) = if instruction.StartsWith "TSX" then Some(this.ParseOperand(instruction.Substring(4))) else None
            let (|TXA|_|) (instruction: string) = if instruction.StartsWith "TXA" then Some(this.ParseOperand(instruction.Substring(4))) else None
            let (|TXS|_|) (instruction: string) = if instruction.StartsWith "TXS" then Some(this.ParseOperand(instruction.Substring(4))) else None
            let (|TYA|_|) (instruction: string) = if instruction.StartsWith "TYA" then Some(this.ParseOperand(instruction.Substring(4))) else None

            //Get the instruction and execute it
            match instruction with
//                | ADC args -> this.ADC(args)
//                | AND args -> this.AND(args)
//                | ASL args -> this.ASL(args)
//                | BCC args -> this.BCC(args)
//                | BCS args -> this.BCS(args)
//                | BEQ args -> this.BEQ(args)
//                | BIT args -> this.BIT(args)
//                | BMI args -> this.BMI(args)
//                | BNE args -> this.BNE(args)
//                | BPL args -> this.BPL(args)
//                | BRK args -> this.BRK(args)
//                | BVC args -> this.BVC(args)
//                | BVS args -> this.BVS(args)
//                | CLC args -> this.CLC(args)
//                | CLD args -> this.CLD(args)
//                | CLI args -> this.CLI(args)
//                | CLV args -> this.CLV(args)
//                | CMP args -> this.CMP(args)
//                | CPX args -> this.CPX(args)
//                | CPY args -> this.CPY(args)
//                | DEC args -> this.DEC(args)
//                | DEX args -> this.DEX(args)
//                | DEY args -> this.DEY(args)
//                | EOR args -> this.EOR(args)
//                | INC args -> this.INC(args)
//                | INX args -> this.INX(args)
//                | INY args -> this.INY(args)
//                | JMP args -> this.JMP(args)
//                | JSR args -> this.JSR(args)
            | LDA args -> this.LDA(args)
//                | LDX args -> this.LDX(args)
//                | LDY args -> this.LDY(args)
//                | LSR args -> this.LSR(args)
//                | NOP args -> this.NOP(args)
//                | ORA args -> this.ORA(args)
//                | PHA args -> this.PHA(args)
//                | PHP args -> this.PHP(args)
//                | PLA args -> this.PLA(args)
//                | PLP args -> this.PLP(args)
//                | ROL args -> this.ROL(args)
//                | ROR args -> this.ROR(args)
//                | RTI args -> this.RTI(args)
//                | RTS args -> this.RTS(args)
//                | SBC args -> this.SBC(args)
//                | SEC args -> this.SEC(args)
//                | SED args -> this.SED(args)
//                | SEI args -> this.SEI(args)
            | STA args -> this.STA(args)
//                | STX args -> this.STX(args)
//                | STY args -> this.STY(args)
//                | TAX args -> this.TAX(args)
//                | TAY args -> this.TAY(args)
//                | TSX args -> this.TSX(args)
//                | TXA args -> this.TXA(args)
//                | TXS args -> this.TXS(args)
//                | TYA args -> this.TYA(args)
            | _ -> failwith "Unknown opcode"

        member private this.ParseOperand(operand) =
            //Define patterns of operans from which we can infer addressing mode
            let (|RegexGroups|) pattern operand =
                let m = Regex.Match(operand, pattern)
                if (m.Success) then
                    if (m.Groups.Count = 2) then Arguments.One(System.Byte.Parse(m.Groups.[1].Value))
                    else Arguments.Two(System.Byte.Parse(m.Groups.[1].Value), System.Byte.Parse(m.Groups.[2].Value))
                else Arguments.Zero
                    
            match operand with
            | RegexGroups "#\$(\d{2})" args -> Modes.Immediate(args)
            | RegexGroups "\$(\d{2})" args -> Modes.ZeroPage(args)
            | RegexGroups "\$(\d{2}),X" args -> Modes.ZeroPageX(args)
            | RegexGroups "\$(\d{2}\d{2})" args -> Modes.Absolute(args)
            | RegexGroups "\$(\d{2}\d{2}),X" args -> Modes.AbsoluteX(args)
            | RegexGroups "\$(\d{2}\d{2}),Y" args -> Modes.AbsoluteY(args)
            | RegexGroups "\(\$(\d{2}),X\)" args -> Modes.IndirectX(args)
            | RegexGroups "\(\$(\d{2})\),Y" args -> Modes.IndirectX(args)
            | _ -> failwith "Cannot parse operand"
                
//                match operand with
//                | 
//                let m = Regex("\[A-Z]{3} \w*,\w*").Match(instruction)
//                if m.Success then Some (List.tail [ for x in m.Groups -> x.Value ])
//                else None

        //Operations
        member private this.LDA mode = 
            match mode with
            | Modes.Immediate(Arguments.One(arg)) -> this.Registers.Accumulator <- arg
            | _ -> failwith "Not supported"

        member private this.STA mode =
            match mode with
            | Modes.Immediate(Arguments.One(arg)) -> this.Memory.[arg] <- this.Registers.Accumulator
            | _ -> failwith "Not supported"

//        member private this.Adc operand = 
//            try
//                let result = Checked.(+) this.Accumulator operand
//                if result = 0uy then this.Zero <- true
//            with e ->
//                this.Overflow <- true

//        member this.And operand =
//            this.Accumulator <- this.Accumulator & operand
//            if this.Accumulator = 0 then this.Zero <- true