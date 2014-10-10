namespace Assembler
    open System
    open System.Text.RegularExpressions

    type private AddressingModes =
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

    type Assembler(startingAddress0) =
        let mutable startingAddress = startingAddress0 //1536us
        new() = Assembler(0us)

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
            let instructions = lines |> Seq.toList |> List.choose this.getInstruction |> List.map this.AssembleInstruction

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
            |> List.toSeq


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
                    if String.IsNullOrWhiteSpace(arguments) then AddressingModes.Implied
                    else
                        //Immediate
                        let mutable m = Regex.Match(arguments, "#\$?([0-9a-fA-F]{1,2})")
                        if (m.Success) then AddressingModes.Immediate(getMatch m)
                        else
                            //Indirect, X
                            let m = Regex.Match(arguments, "\(\$([0-9a-fA-F]{2}),X\)")
                            if (m.Success) then AddressingModes.IndirectX(getMatch m)
                            else
                                //Indirect, Y
                                let m = Regex.Match(arguments, "\(\$([0-9a-fA-F]{2})\),Y")
                                if (m.Success) then AddressingModes.IndirectY(getMatch m)
                                else
                                    //Indirect
                                    let m = Regex.Match(arguments, "\(\$([0-9a-fA-F]{2})([0-9a-fA-F]{2})\)")
                                    if (m.Success) then AddressingModes.Indirect(getMatches m)
                                    else
                                        //ZeroPage, X
                                        let m = Regex.Match(arguments, "\$([0-9a-fA-F]{2}),X")
                                        if (m.Success) then AddressingModes.ZeroPageX(getMatch m)
                                        else
                                            //ZeroPage, Y
                                            let m = Regex.Match(arguments, "\$([0-9a-fA-F]{2}),Y")
                                            if (m.Success) then AddressingModes.ZeroPageX(getMatch m)
                                            else
                                                //Absolute, X
                                                let m = Regex.Match(arguments, "\$([0-9a-fA-F]{2})([0-9a-fA-F]{2}),X")
                                                if (m.Success) then AddressingModes.AbsoluteX(getMatches m)
                                                else
                                                    //Absolute, Y
                                                    let m = Regex.Match(arguments, "\$([0-9a-fA-F]{2})([0-9a-fA-F]{2}),Y")
                                                    if (m.Success) then AddressingModes.AbsoluteY(getMatches m)
                                                    else
                                                        //Absolute
                                                        let m = Regex.Match(arguments, "\$([0-9a-fA-F]{2})([0-9a-fA-F]{2})")
                                                        if (m.Success) then AddressingModes.Absolute(getMatches m)
                                                        else
                                                            //ZeroPage
                                                            let m = Regex.Match(arguments, "\$([0-9a-fA-F]{2})")
                                                            if (m.Success) then AddressingModes.ZeroPage(getMatch m)
                                                            else
                                                                let m = Regex.Match(arguments, "(\w+)")
                                                                if (m.Success) then AddressingModes.Relative(getMatch m)
                                                                else failwith "Cannot parse arguments"

                let mnemonic = instruction.Substring(0, 3)
                let mode = parseAddressingMode(instruction.Substring(3))
                

                //Get the instruction and execute it
                match mnemonic with
                    | "ADC" -> this.ADC mode
                    | "AND" -> this.AND mode
                    | "ASL" -> this.ASL mode
                    | "BCC" -> this.Branch "BCC" mode
                    | "BCS" -> this.Branch "BCS" mode
                    | "BEQ" -> this.Branch "BEQ" mode
                    | "BIT" -> this.BIT mode
                    | "BMI" -> this.Branch "BMI" mode
                    | "BNE" -> this.Branch "BNE" mode
                    | "BPL" -> this.Branch "BPL" mode
                    | "BRK" -> this.BRK mode
                    | "BVC" -> this.Branch "BVC" mode
                    | "BVS" -> this.Branch "BVS" mode
                    | "CLC" -> this.CLC mode
                    | "CLD" -> this.CLD mode
                    | "CLI" -> this.CLI mode
                    | "CLV" -> this.CLV mode
                    | "CMP" -> this.CMP mode
                    | "CPX" -> this.CPX mode
                    | "CPY" -> this.CPY mode
                    | "DEC" -> this.DEC mode
                    | "DEX" -> this.DEX mode
                    | "DEY" -> this.DEY mode
                    | "EOR" -> this.EOR mode
                    | "INC" -> this.INC mode
                    | "INX" -> this.INX mode
                    | "INY" -> this.INY mode
                    | "JMP" -> this.JMP mode
                    | "JSR" -> this.JSR mode
                    | "LDA" -> this.LDA mode
                    | "LDX" -> this.LDX mode
                    | "LDY" -> this.LDY mode
                    | "LSR" -> this.LSR mode
                    | "NOP" -> this.NOP mode
                    | "ORA" -> this.ORA mode
                    | "PHA" -> this.PHA mode
                    | "PHP" -> this.PHP mode
                    | "PLA" -> this.PLA mode
                    | "PLP" -> this.PLP mode
                    | "ROL" -> this.ROL mode
                    | "ROR" -> this.ROR mode
                    | "RTI" -> this.RTI mode
                    | "RTS" -> this.RTS mode
                    | "SBC" -> this.SBC mode
                    | "SEC" -> this.SEC mode
                    | "SED" -> this.SED mode
                    | "SEI" -> this.SEI mode
                    | "STA" -> this.STA mode
                    | "STX" -> this.STX mode
                    | "STY" -> this.STY mode
                    | "TAX" -> this.TAX mode
                    | "TAY" -> this.TAY mode
                    | "TSX" -> this.TSX mode
                    | "TXA" -> this.TXA mode
                    | "TXS" -> this.TXS mode
                    | "TYA" -> this.TYA mode
                    | _ -> failwith "Mnemonics not supported"

        //Instructions
        //...Branching instructions are grouped
        member private this.Branch mnemonics mode =
            let destination =
                match mode with
                | AddressingModes.Relative(label) -> "_" + label //Add an underscore to the label so it'll be easier to locate later
                | _ -> failwith "Addressing mode not valid for a branch instruction"

            match mnemonics with
            | "BPL" -> [ "10"; destination ]
            | "BMI" -> [ "30"; destination ]
            | "BVC" -> [ "50"; destination ]
            | "BVS" -> [ "70"; destination ]
            | "BCC" -> [ "90"; destination ]
            | "BCS" -> [ "B0"; destination ]
            | "BNE" -> [ "D0"; destination ]
            | "BEQ" -> [ "F0"; destination ]
            | _ -> failwith "Not a branching mnemonic"

        //...ADC - Add with Carry
        member private this.ADC mode =
            match mode with
            | AddressingModes.Immediate(arg) -> [ "69"; arg ]
            | AddressingModes.ZeroPage(arg) -> [ "65"; arg ]
            | AddressingModes.ZeroPageX(arg) -> [ "75"; arg ]
            | AddressingModes.Absolute(arg0, arg1) -> [ "6D"; arg1; arg0 ]
            | AddressingModes.AbsoluteX(arg0, arg1) -> [ "7D"; arg1; arg0 ]
            | AddressingModes.AbsoluteY(arg0, arg1) -> [ "79"; arg1; arg0 ]
            | AddressingModes.IndirectX(arg) -> [ "61"; arg ]
            | AddressingModes.IndirectY(arg) -> [ "71"; arg ]
            | _ -> failwith "Not supported"

        //...AND - Bitwise AND with Accumulator
        member private this.AND mode =
            match mode with
            | AddressingModes.Immediate(arg) -> [ "29"; arg ]
            | AddressingModes.ZeroPage(arg) -> [ "25"; arg ]
            | AddressingModes.ZeroPageX(arg) -> [ "35"; arg ]
            | AddressingModes.Absolute(arg0, arg1) -> [ "2D"; arg1; arg0 ]
            | AddressingModes.AbsoluteX(arg0, arg1) -> [ "3D"; arg1; arg0 ]
            | AddressingModes.AbsoluteY(arg0, arg1) -> [ "39"; arg1; arg0 ]
            | AddressingModes.Indirect(arg0, arg1) -> [ "21"; arg1; arg0 ]
            | _ -> failwith "Not supported"

        //...ASL - Arithmetic Shift Left
        member private this.ASL mode =
            match mode with
            | AddressingModes.Implied -> [ "0A" ] //Documentation refers to an "Accumulator" addressing mode... Seems that "ASL" and "ASL A" are both valid.
            | AddressingModes.ZeroPage(arg) -> [ "06"; arg ]
            | AddressingModes.ZeroPageX(arg) -> [ "16"; arg ]
            | AddressingModes.Absolute(arg0, arg1) -> [ "0E"; arg1; arg0 ]
            | AddressingModes.AbsoluteX(arg0, arg1) -> [ "1E"; arg1; arg0 ]
            | _ -> failwith "Not supported"

        //...BIT - Test BITs
        member private this.BIT mode =
            match mode with
            | AddressingModes.ZeroPage(arg) -> [ "24"; arg ]
            | AddressingModes.Absolute(arg0, arg1) -> [ "2C"; arg1; arg0 ]
            | _ -> failwith "Not supported"

        //...BRK - Break
        member private this.BRK mode =
            match mode with
            | AddressingModes.Implied -> [ "00" ]
            | _ -> failwith "Not supported"

        //...CMP - Compare
        member private this.CMP mode =
            match mode with
            | AddressingModes.Immediate(arg) -> [ "C9"; arg ]
            | AddressingModes.ZeroPage(arg) -> [ "C5"; arg ]
            | AddressingModes.ZeroPageX(arg) -> [ "D5"; arg ]
            | AddressingModes.Absolute(arg0, arg1) -> [ "EC"; arg1; arg0 ]
            | _ -> failwith "Not supported"

        //...CLC - Clear Carry
        member private this.CLC mode =
            match mode with
            | AddressingModes.Implied -> [ "18" ]
            | _ -> failwith "Not supported"
        
        //...CLD - Clear Decimal
        member private this.CLD mode =
            match mode with
            | AddressingModes.Implied -> [ "D8" ]
            | _ -> failwith "Not supported"

        //...CLI - Clear Interrupt
        member private this.CLI mode =
            match mode with
            | AddressingModes.Implied -> [ "58" ]
            | _ -> failwith "Not supported"

        //...CLV - Clear Overflow
        member private this.CLV mode =
            match mode with
            | AddressingModes.Implied -> [ "B8" ]
            | _ -> failwith "Not supported"

        //...CPX - Compare X Register
        member private this.CPX mode =
            match mode with
            | AddressingModes.Immediate(arg) -> [ "E0"; arg ]
            | AddressingModes.ZeroPage(arg) -> [ "E4"; arg ]
            | AddressingModes.Absolute(arg0, arg1) -> [ "EC"; arg1; arg0 ]
            | _ -> failwith "Not supported"

        //...CPY - Compare Y Register
        member private this.CPY mode =
            match mode with
            | AddressingModes.Immediate(arg) -> [ "C0"; arg ]
            | AddressingModes.ZeroPage(arg) -> [ "C4"; arg ]
            | AddressingModes.Absolute(arg0, arg1) -> [ "CC"; arg1; arg0 ]
            | _ -> failwith "Not supported"

        //...DEC - Increment Memory
        member private this.DEC mode =
            match mode with
            | AddressingModes.ZeroPage(arg) -> [ "C6"; arg ]
            | AddressingModes.ZeroPageX(arg) -> [ "D6"; arg ]
            | AddressingModes.Absolute(arg0, arg1) -> [ "C3"; arg1; arg0 ]
            | AddressingModes.AbsoluteX(arg0, arg1) -> [ "DE"; arg1; arg0 ]
            | _ -> failwith "Not supported"

        //...DEX - Decrement X Register
        member private this.DEX mode =
            match mode with
            | AddressingModes.Implied -> [ "CA" ]
            | _ -> failwith "Not supported"

        //...DEY - Decrement Y Register
        member private this.DEY mode =
            match mode with
            | AddressingModes.Implied -> [ "88" ]
            | _ -> failwith "Not supported"

        //...EOR - Bitwise Exclusive OR
        member private this.EOR mode =
            match mode with
            | AddressingModes.Immediate(arg) -> [ "49"; arg ]
            | AddressingModes.ZeroPage(arg) -> [ "45"; arg ]
            | AddressingModes.ZeroPageX(arg) -> [ "55"; arg ]
            | AddressingModes.Absolute(arg0, arg1) -> [ "4D"; arg1; arg0 ]
            | AddressingModes.AbsoluteX(arg0, arg1) -> [ "5D"; arg1; arg0 ]
            | AddressingModes.AbsoluteY(arg0, arg1) -> [ "59"; arg1; arg0 ]
            | AddressingModes.IndirectX(arg) -> [ "41"; arg ]
            | AddressingModes.IndirectY(arg) -> [ "51"; arg ]
            | _ -> failwith "Not supported"

        //...INC - Increment Memory
        member private this.INC mode =
            match mode with
            | AddressingModes.ZeroPage(arg) -> [ "E6"; arg ]
            | AddressingModes.ZeroPageX(arg) -> [ "F6"; arg ]
            | AddressingModes.Absolute(arg0, arg1) -> [ "EE"; arg1; arg0 ]
            | AddressingModes.AbsoluteX(arg0, arg1) -> [ "FE"; arg1; arg0 ]
            | _ -> failwith "Not supported"

        //...INX - Increment X
        member private this.INX mode =
            match mode with
            | AddressingModes.Implied -> [ "E8" ]
            | _ -> failwith "Not supported"

        //...INY - Increment Y
        member private this.INY mode =
            match mode with
            | AddressingModes.Implied -> [ "C8" ]
            | _ -> failwith "Not supported"

        //...JMP - Jump (around!)
        member private this.JMP mode =
            match mode with
            | AddressingModes.Absolute(arg0, arg1) -> [ "4C"; arg1; arg0 ]
            | AddressingModes.Indirect(arg0, arg1) -> [ "6C"; arg1; arg0 ]
            | AddressingModes.Relative(label) -> [ "4C"; "_" + label; "" ] //Abuse, absolute with a label will match the relative pattern
            | _ -> failwith "Not supported"

        //...JMP - Jump to Subroutine
        member private this.JSR mode =
            match mode with
            | AddressingModes.Absolute(arg0, arg1) -> [ "20"; arg1; arg0 ]
            | AddressingModes.Relative(label) -> [ "20"; "_" + label; "" ] //Abuse, absolute with a label will match the relative pattern
            | _ -> failwith "Not supported"

        //...LAD - Load Accumulator
        member private this.LDA mode = 
            match mode with
            | AddressingModes.Immediate(arg) -> [ "A9"; arg ]
            | AddressingModes.ZeroPage(arg) -> [ "A5"; arg ]
            | AddressingModes.ZeroPageX(arg) -> [ "B5"; arg ]
            | AddressingModes.Absolute(arg0, arg1) -> [ "AD"; arg1; arg0 ]
            | AddressingModes.AbsoluteX(arg0, arg1) -> [ "BD"; arg1; arg0 ]
            | AddressingModes.AbsoluteY(arg0, arg1) -> [ "B9"; arg1; arg0 ]
            | AddressingModes.IndirectX(arg) -> [ "A1"; arg ]
            | AddressingModes.IndirectY(arg) -> [ "B1"; arg ]
            | _ -> failwith "Not supported"

        //...LDX - Load X Register
        member private this.LDX mode =
            match mode with
            | AddressingModes.Immediate(arg) -> [ "A2"; arg ]
            | AddressingModes.ZeroPage(arg) -> [ "A6"; arg ]
            | AddressingModes.ZeroPageY(arg) -> [ "B6"; arg ]
            | AddressingModes.Absolute(arg0, arg1) -> [ "AE"; arg1; arg0 ]
            | AddressingModes.AbsoluteY(arg0, arg1) -> [ "BE"; arg1; arg0 ]
            | _ -> failwith "Not supported"

        //...LDY - Load Y Register
        member private this.LDY mode =
            match mode with
            | AddressingModes.Immediate(arg) -> [ "A0"; arg ]
            | AddressingModes.ZeroPage(arg) -> [ "A4"; arg ]
            | AddressingModes.ZeroPageX(arg) -> [ "B4"; arg ]
            | AddressingModes.Absolute(arg0, arg1) -> [ "AC"; arg1; arg0 ]
            | AddressingModes.AbsoluteX(arg0, arg1) -> [ "BC"; arg1; arg0 ]
            | _ -> failwith "Not supported"

        //...LSR - Logical Shift Right
        member private this.LSR mode =
            match mode with
            | AddressingModes.Implied -> [ "4A" ] //Documentation refers to an "Accumulator" addressing mode... Seems that "LSR" and "LSR A" are both valid.
            | AddressingModes.ZeroPage(arg) -> [ "46"; arg ]
            | AddressingModes.ZeroPageX(arg) -> [ "56"; arg ]
            | AddressingModes.Absolute(arg0, arg1) -> [ "4E"; arg1; arg0 ]
            | AddressingModes.AbsoluteX(arg0, arg1) -> [ "5E"; arg1; arg0 ]
            | _ -> failwith "Not supported"

        //...NOP - No Operation
        member private this.NOP mode =
            match mode with
            | AddressingModes.Implied -> [ "EA" ]
            | _ -> failwith "Not supported"

        //...ORA - Bitwise OR with Accumulator
        member private this.ORA mode =
            match mode with
            | AddressingModes.Immediate(arg) -> [ "09"; arg ]
            | AddressingModes.ZeroPage(arg) -> [ "05"; arg ]
            | AddressingModes.ZeroPageX(arg) -> [ "15"; arg ]
            | AddressingModes.Absolute(arg0, arg1) -> [ "0D"; arg1; arg0 ]
            | AddressingModes.AbsoluteX(arg0, arg1) -> [ "1D"; arg1; arg0 ]
            | AddressingModes.AbsoluteY(arg0, arg1) -> [ "19"; arg1; arg0 ]
            | AddressingModes.IndirectX(arg) -> [ "01"; arg ]
            | AddressingModes.IndirectY(arg) -> [ "11"; arg ]
            | _ -> failwith "Not supported"

        //...PHA - Push Accumulator
        member private this.PHA mode =
            match mode with
            | AddressingModes.Implied -> [ "48" ]
            | _ -> failwith "Not supported"

        //...PHP - Push Processor status
        member private this.PHP mode =
            match mode with
            | AddressingModes.Implied -> [ "08" ]
            | _ -> failwith "Not supported"

        //...PLA - Pull Accumulator
        member private this.PLA mode =
            match mode with
            | AddressingModes.Implied -> [ "68" ]
            | _ -> failwith "Not supported"

        //...PLP - Pull Processor status
        member private this.PLP mode =
            match mode with
            | AddressingModes.Implied -> [ "28" ]
            | _ -> failwith "Not supported"

        //... ROL - Rotata Left
        member private this.ROL mode =
            match mode with
            | AddressingModes.Implied -> [ "2A" ] //Documentation refers to an "Accumulator" addressing mode... Seems that "ROL" and "ROL A" are both valid.
            | AddressingModes.ZeroPage(arg) -> [ "26"; arg ]
            | AddressingModes.ZeroPageX(arg) -> [ "36"; arg ]
            | AddressingModes.Absolute(arg0, arg1) -> [ "2E"; arg1; arg0 ]
            | AddressingModes.AbsoluteX(arg0, arg1) -> [ "3E"; arg1; arg0 ]
            | _ -> failwith "Not supported"

        //... ROR - Rotata Right
        member private this.ROR mode =
            match mode with
            | AddressingModes.Implied -> [ "6A" ] //Documentation refers to an "Accumulator" addressing mode... Seems that "ROR" and "ROR A" are both valid.
            | AddressingModes.ZeroPage(arg) -> [ "66"; arg ]
            | AddressingModes.ZeroPageX(arg) -> [ "76"; arg ]
            | AddressingModes.Absolute(arg0, arg1) -> [ "6E"; arg1; arg0 ]
            | AddressingModes.AbsoluteX(arg0, arg1) -> [ "7E"; arg1; arg0 ]
            | _ -> failwith "Not supported"

        //...RTI - Return from Interrupt
        member private this.RTI mode =
            match mode with
            | AddressingModes.Implied -> [ "40" ]
            | _ -> failwith "Not supported"

        //...RTS - Return from Subroutine
        member private this.RTS mode =
            match mode with
            | AddressingModes.Implied -> [ "60" ]
            | _ -> failwith "Not supported"

        //...SBC - Substract with Carry
        member private this.SBC mode =
            match mode with
            | AddressingModes.Immediate(arg) -> [ "E9"; arg ]
            | AddressingModes.ZeroPage(arg) -> [ "E5"; arg ]
            | AddressingModes.ZeroPageX(arg) -> [ "F5"; arg ]
            | AddressingModes.Absolute(arg0, arg1) -> [ "ED"; arg1; arg0 ]
            | AddressingModes.AbsoluteX(arg0, arg1) -> [ "FD"; arg1; arg0 ]
            | AddressingModes.AbsoluteY(arg0, arg1) -> [ "F9"; arg1; arg0 ]
            | AddressingModes.IndirectX(arg) -> [ "E1"; arg ]
            | AddressingModes.IndirectY(arg) -> [ "F1"; arg ]
            | _ -> failwith "Not supported"

        //...SEC - Set Carry
        member private this.SEC mode =
            match mode with
            | AddressingModes.Implied -> [ "38" ]
            | _ -> failwith "Not supported"

        //...SED - Set Decimal
        member private this.SED mode =
            match mode with
            | AddressingModes.Implied -> [ "F8" ]
            | _ -> failwith "Not supported"

        //...SEI - Set Interrupt
        member private this.SEI mode =
            match mode with
            | AddressingModes.Implied -> [ "78" ]
            | _ -> failwith "Not supported"

        //...STA - Store A Register
        member private this.STA mode =
            match mode with
            | AddressingModes.ZeroPage(arg) -> [ "85"; arg ]
            | AddressingModes.ZeroPageX(arg) -> [ "95"; arg ]
            | AddressingModes.Absolute(arg0, arg1) -> [ "8D"; arg1; arg0 ]
            | AddressingModes.AbsoluteX(arg0, arg1) -> [ "9D"; arg1; arg0 ]
            | AddressingModes.AbsoluteY(arg0, arg1) -> [ "99"; arg1; arg0 ]
            | AddressingModes.IndirectX(arg) -> [ "81"; arg ]
            | AddressingModes.IndirectY(arg) -> [ "91"; arg ]
            | _ -> failwith "Not supported"

        //...STX - Store X Register
        member private this.STX mode =
            match mode with
            | AddressingModes.ZeroPage(arg) -> [ "86"; arg ]
            | AddressingModes.ZeroPageY(arg) -> [ "96"; arg ]
            | AddressingModes.Absolute(arg0, arg1) -> [ "8E"; arg1; arg0 ]
            | _ -> failwith "Not supported"

        //...STY - Store Y Register
        member private this.STY mode =
            match mode with
            | AddressingModes.ZeroPage(arg) -> [ "84"; arg ]
            | AddressingModes.ZeroPageX(arg) -> [ "94"; arg ]
            | AddressingModes.Absolute(arg0, arg1) -> [ "8C"; arg1; arg0 ]
            | _ -> failwith "Not supported"

        //...TAX - Transfer Accumulator to X
        member private this.TAX mode =
            match mode with
            | AddressingModes.Implied -> [ "AA" ]
            | _ -> failwith "Not supported"

        //...TAY - Transfer Accumulator to Y
        member private this.TAY mode =
            match mode with
            | AddressingModes.Implied -> [ "A8" ]
            | _ -> failwith "Not supported"

        //...TSX - Transfer Stack pointer to X
        member private this.TSX mode =
            match mode with
            | AddressingModes.Implied -> [ "BA" ]
            | _ -> failwith "Not supported"

        //...TXA - Transfer X to Accumulator
        member private this.TXA mode =
            match mode with
            | AddressingModes.Implied -> [ "8A" ]
            | _ -> failwith "Not supported"

        //...TYA - Transfer Y to Accumulator
        member private this.TYA mode =
            match mode with
            | AddressingModes.Implied -> [ "98" ]
            | _ -> failwith "Not supported"

        //...TXS - Transfer X to Stack pointer
        member private this.TXS mode =
            match mode with
            | AddressingModes.Implied -> [ "9A" ]
            | _ -> failwith "Not supported"