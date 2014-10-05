module Assembler
    open System
    open System.Text.RegularExpressions

    type private Mnemonics =
        | ADC = 1
        | AND = 2
        | ASL = 3
        | BCC = 4
        | BCS = 5
        | BEQ = 6
        | BIT = 7
        | BMI = 8
        | BNE = 9
        | BPL = 10
        | BRK = 11
        | BVC = 12
        | BVS = 13
        | CLC = 14
        | CLD = 15
        | CLI = 16
        | CLV = 17
        | CMP = 18
        | CPX = 19
        | CPY = 20
        | DEC = 21
        | DEX = 22
        | DEY = 23
        | EOR = 24
        | INC = 25
        | INX = 26
        | INY = 27
        | JMP = 28
        | JSR = 29
        | LDA = 30
        | LDX = 31
        | LDY = 32
        | LSR = 33
        | NOP = 34
        | ORA = 35
        | PHA = 36
        | PHP = 37
        | PLA = 38
        | PLP = 39
        | ROL = 40
        | ROR = 41
        | RTI = 42
        | RTS = 43
        | SBC = 44
        | SEC = 45
        | SED = 46
        | SEI = 47
        | STA = 48
        | STX = 49
        | STY = 50
        | TAX = 51
        | TAY = 52
        | TSX = 53
        | TXA = 54
        | TXS = 55
        | TYA = 56

    type private Modes =
        | Implied
        | Immediate of byte
        | ZeroPage of byte
        | ZeroPageX of byte
        | ZeroPageY of byte
        | Absolute of byte * byte
        | AbsoluteX of byte * byte
        | AbsoluteY of byte * byte
        | IndirectX of byte
        | IndirectY of byte
        | Relative of string

    type Assembler() =
        let getByte hex =
            if hex = null then failwith "Instruction not supported"
            else Byte.Parse(hex, Globalization.NumberStyles.HexNumber)

        //Used to map the mode to the right opcode
        let getOpcodeForMode mode (opcodes: string[]) =
            match mode with
            | Modes.Implied -> [ getByte opcodes.[0] ]
            | Modes.Immediate(arg) -> [ getByte opcodes.[1]; arg ]
            | Modes.ZeroPage(arg) -> [ getByte opcodes.[2]; arg ]
            | Modes.ZeroPageX(arg) -> [ getByte opcodes.[3]; arg ]
            | Modes.ZeroPageY(arg) -> [ getByte opcodes.[4]; arg ]
            | Modes.Absolute(arg0, arg1) -> [ getByte opcodes.[5]; arg1; arg0 ]
            | Modes.AbsoluteX(arg0, arg1) -> [ getByte opcodes.[6]; arg1; arg0 ]
            | Modes.AbsoluteY(arg0, arg1) -> [ getByte opcodes.[7]; arg1; arg0 ]
            | Modes.IndirectX(arg) -> [ getByte opcodes.[8]; arg ]
            | Modes.IndirectY(arg) -> [ getByte opcodes.[9]; arg ]
            | _ -> failwith "Mode not supported"


        //Program Counter management
        let mutable programCounter = 0us

        member private this.AdvanceProgramCounter count =
            programCounter <- programCounter + count
        
        member private this.ProgramCounter =
            let bytes = BitConverter.GetBytes programCounter
            (bytes.[0], bytes.[1])


        //Label management
        member val private Labels : Map<String, (byte * byte)> = Map.empty with get, set

        member private this.AddLabel label =
            this.Labels <- this.Labels.Add (label, this.ProgramCounter)

        member private this.GetLabelAddress label =
            match this.Labels.TryFind label with
            | Some(address) -> address
            | None -> failwith "Label refered to was not previously declared"


        member private this.Reset =
            programCounter <- 0us
            this.Labels <- Map.empty


        member public this.Assemble lines =
            this.Reset
            lines
            |> List.collect this.Execute
            |> List.toArray
            |> BitConverter.ToString

        //Parse a line and map it to an instruction
        //See Mnemonics and Opcodes here: http://www.6502.org/tutorials/6502opcodes.html
        member private this.Execute (line: string) =
            let instruction =
                if line.Contains(";") then line.Substring(0, (line.IndexOf ';')).Trim() //Strip comments and trim
                else line.Trim()

            if Regex.IsMatch(instruction, "\w+:") then
                //The line is not an instruction, but a label.
                //The current address needs to be registered as the pointer to that label
                let label = Regex.Match(instruction, "(\w+):").Groups.[1].Value
                this.AddLabel label
                []
            else
                //The line is a regular instruction
                let parseAddressingMode arguments =     
                    let parseByte (m: Match) = Byte.Parse(m.Groups.[1].Value, Globalization.NumberStyles.HexNumber)
                    let parseBytes (m: Match) = (Byte.Parse(m.Groups.[1].Value, Globalization.NumberStyles.HexNumber), Byte.Parse(m.Groups.[2].Value, Globalization.NumberStyles.HexNumber))

                    //Implied
                    if String.IsNullOrWhiteSpace(arguments) then Modes.Implied
                    else
                        //Immediate
                        let mutable m = Regex.Match(arguments, "#\$([0-9a-fA-F]{2})")
                        if (m.Success) then Modes.Immediate(parseByte m)
                        else
                            //Indirect, X
                            let m = Regex.Match(arguments, "\(\$([0-9a-fA-F]{2}),X\)")
                            if (m.Success) then Modes.IndirectX(parseByte m)
                            else
                                //Indirect, Y
                                let m = Regex.Match(arguments, "\(\$([0-9a-fA-F]{2})\),Y")
                                if (m.Success) then Modes.IndirectY(parseByte m)
                                else
                                    //ZeroPage, X
                                    let m = Regex.Match(arguments, "\$([0-9a-fA-F]{2}),X")
                                    if (m.Success) then Modes.ZeroPageX(parseByte m)
                                    else
                                        //ZeroPage, Y
                                        let m = Regex.Match(arguments, "\$([0-9a-fA-F]{2}),Y")
                                        if (m.Success) then Modes.ZeroPageX(parseByte m)
                                        else
                                            //Absolute, X
                                            let m = Regex.Match(arguments, "\$([0-9a-fA-F]{2})([0-9a-fA-F]{2}),X")
                                            if (m.Success) then Modes.AbsoluteX(parseBytes m)
                                            else
                                                //Absolute, Y
                                                let m = Regex.Match(arguments, "\$([0-9a-fA-F]{2})([0-9a-fA-F]{2}),Y")
                                                if (m.Success) then Modes.AbsoluteY(parseBytes m)
                                                else
                                                    //Absolute
                                                    let m = Regex.Match(arguments, "\$([0-9a-fA-F]{2})([0-9a-fA-F]{2})")
                                                    if (m.Success) then Modes.Absolute(parseBytes m)
                                                    else
                                                        //ZeroPage
                                                        let m = Regex.Match(arguments, "\$([0-9a-fA-F]{2})")
                                                        if (m.Success) then Modes.ZeroPage(parseByte m)
                                                        else
                                                            let m = Regex.Match(arguments, "(\w+)")
                                                            if (m.Success) then Modes.Relative(m.Groups.[1].Value)
                                                            else failwith "Cannot parse arguments"

                let mode = parseAddressingMode(instruction.Substring 3)
                let mnemonic = Enum.Parse(typeof<Mnemonics>, instruction.Substring(0, 3)) :?> Mnemonics

                //Get the instruction and execute it
                match mnemonic with
    //                | ADC args -> this.ADC(args)
    //                | AND args -> this.AND(args)
    //                | ASL args -> this.ASL(args)
                    | Mnemonics.BCC args -> this.Branch Mnemonics.BCC mode
                    | Mnemonics.BCS args -> this.Branch Mnemonics.BCS mode
                    | Mnemonics.BEQ args -> this.Branch Mnemonics.BEQ mode
    //                | BIT args -> this.BIT(args)
                    | Mnemonics.BMI args -> this.Branch Mnemonics.BMI mode
                    | Mnemonics.BNE args -> this.Branch Mnemonics.BNE mode
                    | Mnemonics.BPL args -> this.Branch Mnemonics.BPL mode
                    | Mnemonics.BRK args -> this.BRK mode
                    | Mnemonics.BVC args -> this.Branch Mnemonics.BVC mode
                    | Mnemonics.BVS args -> this.Branch Mnemonics.BVS mode
    //                | CLC args -> this.CLC(args)
    //                | CLD args -> this.CLD(args)
    //                | CLI args -> this.CLI(args)
    //                | CLV args -> this.CLV(args)
                    | Mnemonics.CMP -> this.CMP mode
                    | Mnemonics.CPX -> this.CPX mode
                    | Mnemonics.CPY -> this.CPY mode
    //                | DEC args -> this.DEC(args)
                    | Mnemonics.DEX -> this.DEX mode
                    | Mnemonics.DEY -> this.DEY mode
    //                | EOR args -> this.EOR(args)
    //                | INC args -> this.INC(args)
    //                | INX args -> this.INX(args)
    //                | INY args -> this.INY(args)
    //                | JMP args -> this.JMP(args)
    //                | JSR args -> this.JSR(args)
                    | Mnemonics.LDA -> this.LDA mode
                    | Mnemonics.LDX -> this.LDX mode
    //                | LDY args -> this.LDY(args)
    //                | LSR args -> this.LSR(args)
                    | Mnemonics.NOP -> this.NOP mode
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
                    | Mnemonics.STA -> this.STA mode
                    | Mnemonics.STX -> this.STX mode
                    | Mnemonics.STY -> this.STY mode
    //                | TAX args -> this.TAX(args)
    //                | TAY args -> this.TAY(args)
    //                | TSX args -> this.TSX(args)
    //                | TXA args -> this.TXA(args)
    //                | TXS args -> this.TXS(args)
    //                | TYA args -> this.TYA(args)
                    | _ -> failwith "Mnemonics not supported"

        //Instructions
        //...Branching instructions are grouped
        member private this.Branch mnemonics mode =
            let destination =
                match mode with
                | Modes.Relative(label) -> this.GetLabelAddress label
                | _ -> failwith "Addressing mode not valid for a branch instruction"

            match mnemonics with
            | Mnemonics.BPL -> [ getByte "10"; fst destination; snd destination ] //TODO: fetch the address of the label
            | Mnemonics.BMI -> [ getByte "30"; fst destination; snd destination ] //TODO: fetch the address of the label
            | Mnemonics.BVC -> [ getByte "50"; fst destination; snd destination ] //TODO: fetch the address of the label
            | Mnemonics.BVS -> [ getByte "70"; fst destination; snd destination ] //TODO: fetch the address of the label
            | Mnemonics.BCC -> [ getByte "90"; fst destination; snd destination ] //TODO: fetch the address of the label
            | Mnemonics.BCS -> [ getByte "B0"; fst destination; snd destination ] //TODO: fetch the address of the label
            | Mnemonics.BNE -> [ getByte "D0"; fst destination; snd destination ] //TODO: fetch the address of the label
            | Mnemonics.BEQ -> [ getByte "F0"; fst destination; snd destination ] //TODO: fetch the address of the label
            | _ -> failwith "Not a branching mnemonic"

        //...BRK - Break
        member private this.BRK mode = getOpcodeForMode mode [| "00"; null; null; null; null; null; null; null; null; null |]

        //...CMP - Compare
        member private this.CMP mode =
            
            getOpcodeForMode mode [| null; "C9"; "C5"; "D4"; null; "EC"; null; null; null; null |]
//            match mode with
//            | Modes.Immediate(arg) -> [ getByte "C9"; arg ]
//            | Modes.ZeroPage(arg) -> [ getByte "C5"; arg ]
//            | Modes.ZeroPageX(arg) -> [ getByte "D5"; arg ]
//            | Modes.Absolute(arg0, arg1) -> [ getByte "EC"; arg1; arg0 ]
//            | _ -> failwith "Not supported"

        //...CPX - Compare X Register
        member private this.CPX mode =
            match mode with
            | Modes.Immediate(arg) -> [ getByte "E0"; arg ]
            | Modes.ZeroPage(arg) -> [ getByte "E4"; arg ]
            | Modes.Absolute(arg0, arg1) -> [ getByte "EC"; arg1; arg0 ]
            | _ -> failwith "Not supported"

        //...CPY - Compare Y Register
        member private this.CPY mode =
            match mode with
            | Modes.Immediate(arg) -> [ getByte "C0"; arg ]
            | Modes.ZeroPage(arg) -> [ getByte "C4"; arg ]
            | Modes.Absolute(arg0, arg1) -> [ getByte "CC"; arg1; arg0 ]
            | _ -> failwith "Not supported"

        //...DEX - Decrement X Register
        member private this.DEX mode =
            match mode with
            | Modes.Implied -> [ getByte "CA" ]
            | _ -> failwith "Not supported"

        //...DEY - Decrement Y Register
        member private this.DEY mode =
            match mode with
            | Modes.Implied -> [ getByte "88" ]
            | _ -> failwith "Not supported"

        //...LAD - Load Accumulator
        member private this.LDA mode = 
            match mode with
            | Modes.Immediate(arg) -> [ getByte "A9"; arg ]
            | Modes.ZeroPage(arg) -> [ getByte "A5"; arg ]
            | Modes.ZeroPageX(arg) -> [ getByte "B5"; arg ]
            | Modes.Absolute(arg0, arg1) -> [ getByte "AD"; arg1; arg0 ]
            | Modes.AbsoluteX(arg0, arg1) -> [ getByte "BD"; arg1; arg0 ]
            | Modes.AbsoluteY(arg0, arg1) -> [ getByte "B9"; arg1; arg0 ]
            | Modes.IndirectX(arg) -> [ getByte "A1"; arg ]
            | Modes.IndirectY(arg) -> [ getByte "B1"; arg ]
            | _ -> failwith "Not supported"

        //...LDX - Load X Register
        member private this.LDX mode =
            match mode with
            | Modes.Immediate(arg) -> [ getByte "A2"; arg ]
            | Modes.ZeroPage(arg) -> [ getByte "A6"; arg ]
            | Modes.ZeroPageY(arg) -> [ getByte "B6"; arg ]
            | Modes.Absolute(arg0, arg1) -> [ getByte "AE"; arg1; arg0 ]
            | Modes.AbsoluteY(arg0, arg1) -> [ getByte "BE"; arg1; arg0 ]
            | _ -> failwith "Not supported"

        //...NOP - No Operation
        member private this.NOP mode =
            match mode with
            | Modes.Implied -> [ getByte "EA" ]
            | _ -> failwith "Not supported"

        //...STA - Store A Register
        member private this.STA mode =
            match mode with
            | Modes.ZeroPage(arg) -> [ getByte "85"; arg ]
            | Modes.ZeroPageX(arg) -> [ getByte "95"; arg ]
            | Modes.Absolute(arg0, arg1) -> [ getByte "8D"; arg1; arg0 ]
            | Modes.AbsoluteX(arg0, arg1) -> [ getByte "9D"; arg1; arg0 ]
            | Modes.AbsoluteY(arg0, arg1) -> [ getByte "99"; arg1; arg0 ]
            | Modes.IndirectX(arg) -> [ getByte "81"; arg ]
            | Modes.IndirectY(arg) -> [ getByte "91"; arg ]
            | _ -> failwith "Not supported"

        //...STX - Store X Register
        member private this.STX mode =
            match mode with
            | Modes.ZeroPage(arg) -> [ getByte "86"; arg ]
            | Modes.ZeroPageY(arg) -> [ getByte "96"; arg ]
            | Modes.Absolute(arg0, arg1) -> [ getByte "8E"; arg1; arg0 ]
            | _ -> failwith "Not supported"

        //...STY - Store Y Register
        member private this.STY mode =
            match mode with
            | Modes.ZeroPage(arg) -> [ getByte "84"; arg ]
            | Modes.ZeroPageX(arg) -> [ getByte "94"; arg ]
            | Modes.Absolute(arg0, arg1) -> [ getByte "8C"; arg1; arg0 ]
            | _ -> failwith "Not supported"