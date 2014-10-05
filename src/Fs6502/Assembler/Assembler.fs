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
        | Immediate of string
        | ZeroPage of string
        | ZeroPageX of string
        | ZeroPageY of string
        | Absolute of string * string
        | AbsoluteX of string * string
        | AbsoluteY of string * string
        | IndirectX of string
        | IndirectY of string
        | Relative of string

    type Assembler() =
        let startingAddress = 0us

        let mutable labels : Map<String, UInt16> = Map.empty

        member private this.AddLabel label index =
            labels <- labels.Add (label, index)

        member private this.GetLabelAddress label =
            match labels.TryFind label with
            | Some(address) -> address
            | None -> failwith "Label refered to was not previously declared"

        member private this.Reset =
            labels <- Map.empty

        member public this.Assemble lines =
            this.Reset

            let offset = ref 0us;

            lines
            |> List.map this.AssembleLine                                               //Convert each line to the equivalent HEX code, excluding labels
            |> List.choose (fun instruction ->                                          //Indexing labels to retreive their addresses in the next pass
                match (List.tryFind (fun (s: string) -> s.StartsWith(":")) instruction) with
                | Some label ->                                                         //If it is a label, index it at the current address and DO NOT increment the program counter (offset)
                    this.AddLabel (label.Substring 1) (startingAddress + !offset)
                    None
                | None ->                                                               //Else advance the program counter (offset)
                    offset := !offset + Convert.ToUInt16(instruction.Length)
                    Some instruction)
            |> List.map (fun instruction ->                                             //Replace references to label with the actual address
                match (List.tryFind (fun (s: string) -> s.StartsWith("_")) instruction) with
                | Some label ->
                    let destination = BitConverter.ToString(BitConverter.GetBytes(this.GetLabelAddress(label.Substring 1)))
                    if instruction.Length = 2 then [ instruction.[0]; destination.Substring(0, 2) ]
                    else [ instruction.[0]; destination.Substring(0, 2); destination.Substring(3, 2) ]
                | None -> instruction)
            |> List.collect (fun instruction -> List.map (fun b -> Byte.Parse(b, Globalization.NumberStyles.HexNumber)) instruction)
            |> List.toArray

        //Parse a line and map it to an instruction
        //See Mnemonics and Opcodes here: http://www.6502.org/tutorials/6502opcodes.html
        member private this.AssembleLine (line: string) : string list =
            let instruction =
                if line.Contains(";") then line.Substring(0, (line.IndexOf ';')).Trim() //Strip comments and trim
                else line.Trim()

            if Regex.IsMatch(instruction, "\w+:") then
                //The line is not an instruction, but a label - return it with ':' in front, it will be removed in the second pass
                [ ":" + Regex.Match(instruction, "(\w+):").Groups.[1].Value]
            else
                //The line is a regular instruction
                let parseAddressingMode arguments =     
                    let getMatch (m: Match) = m.Groups.[1].Value
                    let getMatches (m: Match) = (m.Groups.[1].Value, m.Groups.[2].Value)
                    
                    //Implied
                    if String.IsNullOrWhiteSpace(arguments) then Modes.Implied
                    else
                        //Immediate
                        let mutable m = Regex.Match(arguments, "#\$([0-9a-fA-F]{2})")
                        if (m.Success) then Modes.Immediate(getMatch m)
                        else
                            //Indirect, X
                            let m = Regex.Match(arguments, "\(\$([0-9a-fA-F]{2}),X\)")
                            if (m.Success) then Modes.IndirectX(getMatch m)
                            else
                                //Indirect, Y
                                let m = Regex.Match(arguments, "\(\$([0-9a-fA-F]{2})\),Y")
                                if (m.Success) then Modes.IndirectY(getMatch m)
                                else
                                    //ZeroPage, X
                                    let m = Regex.Match(arguments, "\$([0-9a-fA-F]{2}),X")
                                    if (m.Success) then Modes.ZeroPageX(getMatch m)
                                    else
                                        //ZeroPage, Y
                                        let m = Regex.Match(arguments, "\$([0-9a-fA-F]{2}),Y")
                                        if (m.Success) then Modes.ZeroPageX(getMatch m)
                                        else
                                            //Absolute, X
                                            let m = Regex.Match(arguments, "\$([0-9a-fA-F]{2})([0-9a-fA-F]{2}),X")
                                            if (m.Success) then Modes.AbsoluteX(getMatches m)
                                            else
                                                //Absolute, Y
                                                let m = Regex.Match(arguments, "\$([0-9a-fA-F]{2})([0-9a-fA-F]{2}),Y")
                                                if (m.Success) then Modes.AbsoluteY(getMatches m)
                                                else
                                                    //Absolute
                                                    let m = Regex.Match(arguments, "\$([0-9a-fA-F]{2})([0-9a-fA-F]{2})")
                                                    if (m.Success) then Modes.Absolute(getMatches m)
                                                    else
                                                        //ZeroPage
                                                        let m = Regex.Match(arguments, "\$([0-9a-fA-F]{2})")
                                                        if (m.Success) then Modes.ZeroPage(getMatch m)
                                                        else
                                                            let m = Regex.Match(arguments, "(\w+)")
                                                            if (m.Success) then Modes.Relative(getMatch m)
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
                | Modes.Relative(label) -> "_" + label
                | _ -> failwith "Addressing mode not valid for a branch instruction"

            match mnemonics with
            | Mnemonics.BPL -> [ "10"; destination ] //TODO: fetch the address of the label
            | Mnemonics.BMI -> [ "30"; destination ] //TODO: fetch the address of the label
            | Mnemonics.BVC -> [ "50"; destination ] //TODO: fetch the address of the label
            | Mnemonics.BVS -> [ "70"; destination ] //TODO: fetch the address of the label
            | Mnemonics.BCC -> [ "90"; destination ] //TODO: fetch the address of the label
            | Mnemonics.BCS -> [ "B0"; destination ] //TODO: fetch the address of the label
            | Mnemonics.BNE -> [ "D0"; destination ] //TODO: fetch the address of the label
            | Mnemonics.BEQ -> [ "F0"; destination ] //TODO: fetch the address of the label
            | _ -> failwith "Not a branching mnemonic"

        //...BRK - Break
        member private this.BRK mode =
            match mode with
            | Modes.Implied -> [ "00" ]
            | _ -> failwith "Not supported"

        //...CMP - Compare
        member private this.CMP mode =
            match mode with
            | Modes.Immediate(arg) -> [ "C9"; arg ]
            | Modes.ZeroPage(arg) -> [ "C5"; arg ]
            | Modes.ZeroPageX(arg) -> [ "D5"; arg ]
            | Modes.Absolute(arg0, arg1) -> [ "EC"; arg1; arg0 ]
            | _ -> failwith "Not supported"

        //...CPX - Compare X Register
        member private this.CPX mode =
            match mode with
            | Modes.Immediate(arg) -> [ "E0"; arg ]
            | Modes.ZeroPage(arg) -> [ "E4"; arg ]
            | Modes.Absolute(arg0, arg1) -> [ "EC"; arg1; arg0 ]
            | _ -> failwith "Not supported"

        //...CPY - Compare Y Register
        member private this.CPY mode =
            match mode with
            | Modes.Immediate(arg) -> [ "C0"; arg ]
            | Modes.ZeroPage(arg) -> [ "C4"; arg ]
            | Modes.Absolute(arg0, arg1) -> [ "CC"; arg1; arg0 ]
            | _ -> failwith "Not supported"

        //...DEX - Decrement X Register
        member private this.DEX mode =
            match mode with
            | Modes.Implied -> [ "CA" ]
            | _ -> failwith "Not supported"

        //...DEY - Decrement Y Register
        member private this.DEY mode =
            match mode with
            | Modes.Implied -> [ "88" ]
            | _ -> failwith "Not supported"

        //...LAD - Load Accumulator
        member private this.LDA mode = 
            match mode with
            | Modes.Immediate(arg) -> [ "A9"; arg ]
            | Modes.ZeroPage(arg) -> [ "A5"; arg ]
            | Modes.ZeroPageX(arg) -> [ "B5"; arg ]
            | Modes.Absolute(arg0, arg1) -> [ "AD"; arg1; arg0 ]
            | Modes.AbsoluteX(arg0, arg1) -> [ "BD"; arg1; arg0 ]
            | Modes.AbsoluteY(arg0, arg1) -> [ "B9"; arg1; arg0 ]
            | Modes.IndirectX(arg) -> [ "A1"; arg ]
            | Modes.IndirectY(arg) -> [ "B1"; arg ]
            | _ -> failwith "Not supported"

        //...LDX - Load X Register
        member private this.LDX mode =
            match mode with
            | Modes.Immediate(arg) -> [ "A2"; arg ]
            | Modes.ZeroPage(arg) -> [ "A6"; arg ]
            | Modes.ZeroPageY(arg) -> [ "B6"; arg ]
            | Modes.Absolute(arg0, arg1) -> [ "AE"; arg1; arg0 ]
            | Modes.AbsoluteY(arg0, arg1) -> [ "BE"; arg1; arg0 ]
            | _ -> failwith "Not supported"

        //...NOP - No Operation
        member private this.NOP mode =
            match mode with
            | Modes.Implied -> [ "EA" ]
            | _ -> failwith "Not supported"

        //...STA - Store A Register
        member private this.STA mode =
            match mode with
            | Modes.ZeroPage(arg) -> [ "85"; arg ]
            | Modes.ZeroPageX(arg) -> [ "95"; arg ]
            | Modes.Absolute(arg0, arg1) -> [ "8D"; arg1; arg0 ]
            | Modes.AbsoluteX(arg0, arg1) -> [ "9D"; arg1; arg0 ]
            | Modes.AbsoluteY(arg0, arg1) -> [ "99"; arg1; arg0 ]
            | Modes.IndirectX(arg) -> [ "81"; arg ]
            | Modes.IndirectY(arg) -> [ "91"; arg ]
            | _ -> failwith "Not supported"

        //...STX - Store X Register
        member private this.STX mode =
            match mode with
            | Modes.ZeroPage(arg) -> [ "86"; arg ]
            | Modes.ZeroPageY(arg) -> [ "96"; arg ]
            | Modes.Absolute(arg0, arg1) -> [ "8E"; arg1; arg0 ]
            | _ -> failwith "Not supported"

        //...STY - Store Y Register
        member private this.STY mode =
            match mode with
            | Modes.ZeroPage(arg) -> [ "84"; arg ]
            | Modes.ZeroPageX(arg) -> [ "94"; arg ]
            | Modes.Absolute(arg0, arg1) -> [ "8C"; arg1; arg0 ]
            | _ -> failwith "Not supported"