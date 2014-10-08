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
        | Indirect of string * string
        | IndirectX of string
        | IndirectY of string
        | Relative of string

    type Assembler() =
        let startingAddress = 1536us

        member private this.IndexLabels (lines: string list list) =
            let programCounter = ref startingAddress //aka PC
            let labels : Map<String, UInt16> ref = ref Map.empty

            lines |> List.iter (fun instruction ->
                let opcode = instruction.[0]
                if opcode.StartsWith(":") then //"instructions" that are labels start with ":"
                    labels := (!labels).Add ("_" + (opcode.Substring 1), !programCounter) //It is a label, index it at the current address and DO NOT the PC
                else programCounter := !programCounter + Convert.ToUInt16(instruction.Length)) //Advance the PC by the size of the instruction

            labels //Return the dictionary

        member private this.ReplaceLabels (labels: Map<String, UInt16>) (instructions: string list list) =
            //Function to find a label in the label dictionary
            let getLabelAddress label =
                match (labels).TryFind label with
                | Some(address) -> address
                | None -> failwith "Label refered to was not previously declared"

            //Function to calculate the address difference
            let getOffset (source: UInt16) (destination: UInt16) =
                (Convert.ToUInt16 (destination - source)).ToString("X4")

            let programCounter = ref startingAddress //aka PC

            //If the instruction is two bytes (BNE and the likes), the label address is an offset stored in one byte
            //If the instruction is three bytes (JMP, JSR), the label address is absolute in two bytes
            instructions |> List.map (fun instruction ->
                programCounter := !programCounter + Convert.ToUInt16(instruction.Length)
                if instruction.Length > 1 && instruction.[1].StartsWith "_" then
                    let labelAddress = getLabelAddress instruction.[1]
                    match instruction.Length with
                    | 2 -> [ instruction.[0]; (getOffset !programCounter labelAddress).Substring(2, 2) ]
                    | 3 ->
                        let hexAddress = labelAddress.ToString("X4")
                        [ instruction.[0]; hexAddress.Substring(2, 2); hexAddress.Substring(0, 2) ]
                    | _ -> failwith "Incorrect instruction length for a branch or jump"
                else instruction)

        //Check if it is a proper instruction, if so return it trimmed of whitespace and comments and make it uppercase
        member private this.getInstruction (line: string) =
            let mutable l = line
            if line.Contains(";") then l <- line.Substring(0, (line.IndexOf ';')) //Strip comments and trim
            l <- l.Trim()
            if String.IsNullOrWhiteSpace(l) then None
            else Some (l.ToUpperInvariant())

        member public this.Assemble lines =
            //Assemble every line
            //...labels and references to labels are left with their name but marked - ":" in front of a label; "_" in front of a reference to a label
            //...the resulting list is a list of instructions, each instruction being a list of one, two or three bytes
            let instructions = lines |> List.choose this.getInstruction |> List.map this.AssembleInstruction

            //First pass: index the labels, now that we know the program's size and can caculate their exact position
            let labels = this.IndexLabels instructions

            instructions
            //Second pass: remove labels that are not useful anymore
            |> List.filter (fun instruction ->
                let opcode = instruction.[0]
                not (instruction.Length = 1 && opcode.StartsWith(":")))
            //Third pass: replace references to labels by the corresponding address
            |> this.ReplaceLabels !labels
            |> List.collect (fun instruction -> List.map (fun b -> Byte.Parse(b, Globalization.NumberStyles.HexNumber)) instruction)
            |> List.toArray


        //Parse a line and map it to an instruction
        //See Mnemonics and Opcodes here: http://www.6502.org/tutorials/6502opcodes.html
        member private this.AssembleInstruction (instruction: string) : string list =
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
                        let mutable m = Regex.Match(arguments, "#\$?([0-9a-fA-F]{1,2})")
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
                                    //Indirect
                                    let m = Regex.Match(arguments, "\(\$([0-9a-fA-F]{2})([0-9a-fA-F]{2})\)")
                                    if (m.Success) then Modes.Indirect(getMatches m)
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
                    | Mnemonics.ADC -> this.ADC mode
                    | Mnemonics.AND -> this.AND mode
    //                | Mnemonics.ASL -> this.ASL mode
                    | Mnemonics.BCC -> this.Branch Mnemonics.BCC mode
                    | Mnemonics.BCS -> this.Branch Mnemonics.BCS mode
                    | Mnemonics.BEQ -> this.Branch Mnemonics.BEQ mode
                    | Mnemonics.BIT args -> this.BIT mode
                    | Mnemonics.BMI -> this.Branch Mnemonics.BMI mode
                    | Mnemonics.BNE -> this.Branch Mnemonics.BNE mode
                    | Mnemonics.BPL -> this.Branch Mnemonics.BPL mode
                    | Mnemonics.BRK -> this.BRK mode
                    | Mnemonics.BVC -> this.Branch Mnemonics.BVC mode
                    | Mnemonics.BVS -> this.Branch Mnemonics.BVS mode
                    | Mnemonics.CLC -> this.CLC mode
                    | Mnemonics.CLD -> this.CLD mode
                    | Mnemonics.CLI -> this.CLI mode
                    | Mnemonics.CLV -> this.CLV mode
                    | Mnemonics.CMP -> this.CMP mode
                    | Mnemonics.CPX -> this.CPX mode
                    | Mnemonics.CPY -> this.CPY mode
                    | Mnemonics.DEC -> this.DEC mode
                    | Mnemonics.DEX -> this.DEX mode
                    | Mnemonics.DEY -> this.DEY mode
    //                | Mnemonics.EOR -> this.EOR mode
                    | Mnemonics.INC -> this.INC mode
                    | Mnemonics.INX -> this.INX mode
                    | Mnemonics.INY -> this.INY mode
                    | Mnemonics.JMP -> this.JMP mode
                    | Mnemonics.JSR -> this.JSR mode
                    | Mnemonics.LDA -> this.LDA mode
                    | Mnemonics.LDX -> this.LDX mode
                    | Mnemonics.LDY -> this.LDY mode
                    | Mnemonics.LSR -> this.LSR mode
                    | Mnemonics.NOP -> this.NOP mode
    //                | Mnemonics.ORA -> this.ORA mode
                    | Mnemonics.PHA -> this.PHA mode
                    | Mnemonics.PHP -> this.PHP mode
                    | Mnemonics.PLA -> this.PLA mode
                    | Mnemonics.PLP -> this.PLP mode
    //                | Mnemonics.ROL -> this.ROL mode
    //                | Mnemonics.ROR -> this.ROR mode
    //                | Mnemonics.RTI -> this.RTI mode
                    | Mnemonics.RTS -> this.RTS mode
                    | Mnemonics.SBC -> this.SBC mode
                    | Mnemonics.SEC -> this.SEC mode
                    | Mnemonics.SED -> this.SED mode
                    | Mnemonics.SEI -> this.SEI mode
                    | Mnemonics.STA -> this.STA mode
                    | Mnemonics.STX -> this.STX mode
                    | Mnemonics.STY -> this.STY mode
                    | Mnemonics.TAX -> this.TAX mode
    //                | Mnemonics.TAY -> this.TAY mode
                    | Mnemonics.TSX -> this.TSX mode
                    | Mnemonics.TXA -> this.TXA mode
                    | Mnemonics.TXS -> this.TXS mode
    //                | Mnemonics.TYA -> this.TYA mode
                    | _ -> failwith "Mnemonics not supported"

        //Instructions
        //...Branching instructions are grouped
        member private this.Branch mnemonics mode =
            let destination =
                match mode with
                | Modes.Relative(label) -> "_" + label //Add an underscore to the label so it'll be easier to locate later
                | _ -> failwith "Addressing mode not valid for a branch instruction"

            match mnemonics with
            | Mnemonics.BPL -> [ "10"; destination ]
            | Mnemonics.BMI -> [ "30"; destination ]
            | Mnemonics.BVC -> [ "50"; destination ]
            | Mnemonics.BVS -> [ "70"; destination ]
            | Mnemonics.BCC -> [ "90"; destination ]
            | Mnemonics.BCS -> [ "B0"; destination ]
            | Mnemonics.BNE -> [ "D0"; destination ]
            | Mnemonics.BEQ -> [ "F0"; destination ]
            | _ -> failwith "Not a branching mnemonic"

        //...ADC - Add with Carry
        member private this.ADC mode =
            match mode with
            | Modes.Immediate(arg) -> [ "69"; arg ]
            | Modes.ZeroPage(arg) -> [ "65"; arg ]
            | Modes.ZeroPageX(arg) -> [ "75"; arg ]
            | Modes.Absolute(arg0, arg1) -> [ "6D"; arg1; arg0 ]
            | Modes.AbsoluteX(arg0, arg1) -> [ "7D"; arg1; arg0 ]
            | Modes.AbsoluteY(arg0, arg1) -> [ "79"; arg1; arg0 ]
            | Modes.IndirectX(arg) -> [ "61"; arg ]
            | Modes.IndirectY(arg) -> [ "71"; arg ]
            | _ -> failwith "Not supported"

        //...AND - Bitwise AND with Accumulator
        member private this.AND mode =
            match mode with
            | Modes.Immediate(arg) -> [ "29"; arg ]
            | Modes.ZeroPage(arg) -> [ "25"; arg ]
            | Modes.ZeroPageX(arg) -> [ "35"; arg ]
            | Modes.Absolute(arg0, arg1) -> [ "2D"; arg1; arg0 ]
            | Modes.AbsoluteX(arg0, arg1) -> [ "3D"; arg1; arg0 ]
            | Modes.AbsoluteY(arg0, arg1) -> [ "39"; arg1; arg0 ]
            | Modes.Indirect(arg0, arg1) -> [ "21"; arg1; arg0 ]
            | _ -> failwith "Not supported"

        //...BIT - Test BITs
        member private this.BIT mode =
            match mode with
            | Modes.ZeroPage(arg) -> [ "24"; arg ]
            | Modes.Absolute(arg0, arg1) -> [ "2C"; arg1; arg0 ]
            | _ -> failwith "Not supported"

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

        //...CLC - Clear Carry
        member private this.CLC mode =
            match mode with
            | Modes.Implied -> [ "18" ]
            | _ -> failwith "Not supported"
        
        //...CLD - Clear Decimal
        member private this.CLD mode =
            match mode with
            | Modes.Implied -> [ "D8" ]
            | _ -> failwith "Not supported"

        //...CLI - Clear Interrupt
        member private this.CLI mode =
            match mode with
            | Modes.Implied -> [ "58" ]
            | _ -> failwith "Not supported"

        //...CLV - Clear Overflow
        member private this.CLV mode =
            match mode with
            | Modes.Implied -> [ "B8" ]
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

        //...DEC - Increment Memory
        member private this.DEC mode =
            match mode with
            | Modes.ZeroPage(arg) -> [ "C6"; arg ]
            | Modes.ZeroPageX(arg) -> [ "D6"; arg ]
            | Modes.Absolute(arg0, arg1) -> [ "C3"; arg1; arg0 ]
            | Modes.AbsoluteX(arg0, arg1) -> [ "DE"; arg1; arg0 ]
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

        //...INC - Increment Memory
        member private this.INC mode =
            match mode with
            | Modes.ZeroPage(arg) -> [ "E6"; arg ]
            | Modes.ZeroPageX(arg) -> [ "F6"; arg ]
            | Modes.Absolute(arg0, arg1) -> [ "EE"; arg1; arg0 ]
            | Modes.AbsoluteX(arg0, arg1) -> [ "FE"; arg1; arg0 ]
            | _ -> failwith "Not supported"

        //...INX - Increment X
        member private this.INX mode =
            match mode with
            | Modes.Implied -> [ "E8" ]
            | _ -> failwith "Not supported"

        //...INY - Increment Y
        member private this.INY mode =
            match mode with
            | Modes.Implied -> [ "C8" ]
            | _ -> failwith "Not supported"

        //...JMP - Jump (around!)
        member private this.JMP mode =
            match mode with
            | Modes.Absolute(arg0, arg1) -> [ "4C"; arg1; arg0 ]
            | Modes.Indirect(arg0, arg1) -> [ "6C"; arg1; arg0 ]
            | Modes.Relative(label) -> [ "4C"; "_" + label; "" ] //Abuse, absolute with a label will match the relative pattern
            | _ -> failwith "Not supported"

        //...JMP - Jump to Subroutine
        member private this.JSR mode =
            match mode with
            | Modes.Absolute(arg0, arg1) -> [ "20"; arg1; arg0 ]
            | Modes.Relative(label) -> [ "20"; "_" + label; "" ] //Abuse, absolute with a label will match the relative pattern
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

        //...LDY - Load Y Register
        member private this.LDY mode =
            match mode with
            | Modes.Immediate(arg) -> [ "A0"; arg ]
            | Modes.ZeroPage(arg) -> [ "A4"; arg ]
            | Modes.ZeroPageX(arg) -> [ "B4"; arg ]
            | Modes.Absolute(arg0, arg1) -> [ "AC"; arg1; arg0 ]
            | Modes.AbsoluteX(arg0, arg1) -> [ "BC"; arg1; arg0 ]
            | _ -> failwith "Not supported"

        //...LSR - Logical Shift Right
        member private this.LSR mode =
            match mode with
            | Modes.Implied -> [ "4A" ] //Documentation refers to an "Accumulator" addressing mode... Seems that "LSR" and "LSR A" are both valid.
            | Modes.ZeroPage(arg) -> [ "46"; arg ]
            | Modes.ZeroPageX(arg) -> [ "56"; arg ]
            | Modes.Absolute(arg0, arg1) -> [ "4E"; arg1; arg0 ]
            | Modes.AbsoluteX(arg0, arg1) -> [ "5E"; arg1; arg0 ]
            | _ -> failwith "Not supported"

        //...NOP - No Operation
        member private this.NOP mode =
            match mode with
            | Modes.Implied -> [ "EA" ]
            | _ -> failwith "Not supported"

        //...PHA - Push Accumulator
        member private this.PHA mode =
            match mode with
            | Modes.Implied -> [ "48" ]
            | _ -> failwith "Not supported"

        //...PHP - Push Processor status
        member private this.PHP mode =
            match mode with
            | Modes.Implied -> [ "08" ]
            | _ -> failwith "Not supported"

        //...PLA - Pull Accumulator
        member private this.PLA mode =
            match mode with
            | Modes.Implied -> [ "68" ]
            | _ -> failwith "Not supported"

        //...PLP - Pull Processor status
        member private this.PLP mode =
            match mode with
            | Modes.Implied -> [ "28" ]
            | _ -> failwith "Not supported"

        //...RTS - Return from Subroutine
        member private this.RTS mode =
            match mode with
            | Modes.Implied -> [ "60" ]
            | _ -> failwith "Not supported"

        //...SBC - Substract with Carry
        member private this.SBC mode =
            match mode with
            | Modes.Immediate(arg) -> [ "E9"; arg ]
            | Modes.ZeroPage(arg) -> [ "E5"; arg ]
            | Modes.ZeroPageX(arg) -> [ "F5"; arg ]
            | Modes.Absolute(arg0, arg1) -> [ "ED"; arg1; arg0 ]
            | Modes.AbsoluteX(arg0, arg1) -> [ "FD"; arg1; arg0 ]
            | Modes.AbsoluteY(arg0, arg1) -> [ "F9"; arg1; arg0 ]
            | Modes.IndirectX(arg) -> [ "E1"; arg ]
            | Modes.IndirectY(arg) -> [ "F1"; arg ]
            | _ -> failwith "Not supported"

        //...SEC - Set Carry
        member private this.SEC mode =
            match mode with
            | Modes.Implied -> [ "38" ]
            | _ -> failwith "Not supported"

        //...SED - Set Decimal
        member private this.SED mode =
            match mode with
            | Modes.Implied -> [ "F8" ]
            | _ -> failwith "Not supported"

        //...SEI - Set Interrupt
        member private this.SEI mode =
            match mode with
            | Modes.Implied -> [ "78" ]
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

        //...TAX - Transfer Accumulator to X
        member private this.TAX mode =
            match mode with
            | Modes.Implied -> [ "AA" ]
            | _ -> failwith "Not supported"

        //...TSX - Transfer Stack pointer to X
        member private this.TSX mode =
            match mode with
            | Modes.Implied -> [ "BA" ]
            | _ -> failwith "Not supported"

        //...TXA - Transfer X to Accumulator
        member private this.TXA mode =
            match mode with
            | Modes.Implied -> [ "8A" ]
            | _ -> failwith "Not supported"

        //...TXS - Transfer X to Stack pointer
        member private this.TXS mode =
            match mode with
            | Modes.Implied -> [ "9A" ]
            | _ -> failwith "Not supported"