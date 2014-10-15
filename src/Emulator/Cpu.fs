﻿namespace Fs6502.Emulator
    module ByteExtension =
        type System.Byte with
            member this.ToUInt16 = System.BitConverter.ToUInt16([| this; 0uy; |], 0)
            member this.ToInt32 = System.BitConverter.ToInt32([| this; 0uy; 0uy; 0uy |], 0)

    open System
    open ByteExtension


    type Word =
        struct
            val Msb: byte
            val Lsb: byte
            new(msb, lsb) = { Msb = msb; Lsb = lsb }
            new(value: UInt16) =
                let bs = BitConverter.GetBytes(value)
                { Msb = bs.[1]; Lsb = bs.[0] }
        end

        member this.ToUInt16 = BitConverter.ToUInt16([| this.Lsb; this.Msb |], 0)
        member this.ToInt32 = BitConverter.ToInt32([| this.Lsb; this.Msb; 0uy; 0uy |], 0)

        member this.Add (b: byte) = new Word(this.ToUInt16 + b.ToUInt16)
        member this.Substract(b: byte) = new Word(this.ToUInt16 - b.ToUInt16)
        member this.Add (i: UInt16) = new Word(this.ToUInt16 + i)
        member this.Substract (i: UInt16) = new Word(this.ToUInt16 - i)

        member this.ToString = BitConverter.ToString([| this.Msb; this.Lsb |]).Replace("-", "")


    type AddressingModes =
        | Implied
        | Immediate of byte
        | ZeroPage of byte
        | ZeroPageX of byte
        | ZeroPageY of byte
        | Absolute of Word
        | AbsoluteX of Word
        | AbsoluteY of Word
        | Indirect of Word
        | IndirectX of byte
        | IndirectY of byte
        | Relative of byte


    type Flags =
        {
            Carry: bool                 // C
            Zero: bool                  // Z
            Interrupt: bool             // I
            Decimal: bool               // D
            BreakCommand: bool          // B
            _Reserved: bool             // -
            Overflow: bool              // V
            Negative: bool             // N
        }


    type Memory() =
        let memory = Array2D.init 256 256 (fun x y -> 0uy)

        member this.Item
            with get (address: Word) = memory.[address.Msb.ToInt32, address.Lsb.ToInt32]
            and set (address: Word) value = memory.[address.Msb.ToInt32, address.Lsb.ToInt32] <- value


    type Status =
        {
            Accumulator: byte
            X: byte
            Y: byte
            StackPointer: Word
            ProgramCounter: Word
            Flags: Flags
            Memory: Memory
            Cycles: UInt64
        }


    type Cpu() =
        [<Literal>] 
        let StackPageReference = 0x01uy
        [<Literal>] 
        let ZeroPageReference = 0x00uy
        [<Literal>] 
        let StartingStackPointer = 0xFFuy

        //Advancing PC and adding cycles are so common operation that it's handy to have functions to do it
        let apc (status: Status) (count: int) = { status with ProgramCounter = status.ProgramCounter.Add (Convert.ToUInt16 count) } //Advance ProgramCounter
        let ac (status: Status) (count: int) (pageCrossed: bool) = { status with Cycles = status.Cycles + Convert.ToUInt64(count) + (if pageCrossed then 1UL else 0UL) } //Add Cycles

        //Get the address from the Address mode
        let ga mode status = //Get Address
            match mode with
            | ZeroPage(arg) -> new Word(ZeroPageReference, arg), false
            | ZeroPageX(arg) -> new Word(ZeroPageReference, arg + status.X), false //Zero Page wraps around
            | ZeroPageY(arg) -> new Word(ZeroPageReference, arg + status.Y), false //Zero Page wraps around
            | Absolute(arg) -> arg, false
            | AbsoluteX(arg) ->
                let address = arg.Add status.X
                (address, address.Msb > arg.Msb) //Can cross page boundaries
            | AbsoluteY(arg) ->
                let address = arg.Add status.Y
                (address, address.Msb > arg.Msb) //Can cross page boundaries
            | Indirect(arg) -> new Word(status.Memory.[arg.Add 1uy], status.Memory.[arg]), false
            | IndirectX(arg) ->
                let address = new Word(ZeroPageReference, arg + status.X) //Zero Page wraps around
                new Word(status.Memory.[address.Add 1uy], status.Memory.[address]), false
            | IndirectY(arg) ->
                let address = new Word(ZeroPageReference, arg)
                let indirectAddress = new Word(status.Memory.[address.Add 1uy], status.Memory.[address])
                let finalAddress = indirectAddress.Add status.Y
                finalAddress, (finalAddress.Msb > indirectAddress.Msb) //Can cross page boundaries
                //Can cross page boundaries
            | _ -> failwith "Addressing mode is not ZeroPage, Absolute or Indirect"

        //Get value from the Address mode
        let gv mode status =
            match mode with
            | Immediate(arg) -> arg, false
            | _ ->
                let (address, pageCrossed) = ga mode status
                status.Memory.[address], pageCrossed

        member val public Status =
            {
                Accumulator = 0uy
                X = 0uy
                Y = 0uy
                ProgramCounter = new Word(0us);
                StackPointer = new Word(StartingStackPointer, 0uy)
                Flags =
                    {
                        Carry = false
                        Zero = false
                        Interrupt = false
                        Decimal = false
                        BreakCommand = true
                        _Reserved = true
                        Overflow = false
                        Negative = false
                    }
                Memory = new Memory()
                Cycles = 0UL
            }
            with get, set

        member this.Execute entryPoint (machineCode: byte[]) =
            let gba (offset: int) = machineCode.[(this.Status.ProgramCounter.Substract (entryPoint - (Convert.ToUInt16 offset))).ToInt32]  //GetByteAhead
            
            //let endingAddress = ((new Word(startingAddress)).Add Convert.ToUInt16(machineCode.Length)).ToUInt16
            let endPoint = ((new Word(entryPoint)).Add (Convert.ToUInt16 machineCode.Length)).ToUInt16
            this.Status <- apc this.Status (Convert.ToInt32 entryPoint)

            //Main loop
            while this.Status.ProgramCounter.ToUInt16 < endPoint do
                let opcode = gba 0 //Parse the next byte to get the opcode as a string

                //Change in the cpu's status is atomic and no transitional state should be externally observable
                this.Status <-
                    match opcode with
                    //Branches - BPL, BMI, BVC, BVC, BCC, BCS, BNE, BEQ
                    | 0x10uy | 0x30uy | 0x50uy | 0x70uy | 0x90uy | 0xB0uy | 0xD0uy | 0xF0uy ->
                        this.Branch opcode (gba 1) (apc this.Status 2)
                    //Flags manipulations
                    | 0x18uy | 0x38uy | 0x58uy | 0x78uy | 0xB8uy | 0xD8uy | 0xF8uy ->
                        this.Flags opcode (apc this.Status 1)
                    //Registers manipulations
                    | 0xAAuy | 0x8Auy | 0xCAuy | 0xE8uy | 0xA8uy | 0x98uy | 0x88uy | 0xC8uy | 0x9Auy | 0xBAuy ->
                        this.Registers opcode (apc this.Status 1)
                    //Stack manipulations
                    | 0x48uy | 0x68uy | 0x08uy | 0x28uy ->
                        this.Stack opcode (apc this.Status 1)
//                    //ADC - Add with Carry
                    | 0x69uy -> this.ADC (Immediate(gba 1)) (apc this.Status 2) 2
                    | 0x65uy -> this.ADC (ZeroPage(gba 1)) (apc this.Status 2) 3
                    | 0x75uy -> this.ADC (ZeroPageX(gba 1)) (apc this.Status 2) 4
                    | 0x6Duy -> this.ADC (Absolute(new Word(gba 2, gba 1))) (apc this.Status 3) 4
                    | 0x7Duy -> this.ADC (AbsoluteX(new Word(gba 2, gba 1))) (apc this.Status 3) 4
                    | 0x79uy -> this.ADC (AbsoluteY(new Word(gba 2, gba 1))) (apc this.Status 3) 4
                    | 0x61uy -> this.ADC (IndirectX(gba 1)) (apc this.Status 2) 6
                    | 0x71uy -> this.ADC (IndirectY(gba 1)) (apc this.Status 2) 5
//                    //AND - Bitwise AND with Accumulator
//                    | 0x29uy -> this.AND (AddressingModes.Immediate(gba 1)) (apc this.Status 2)
                    //BRK - Force Interrupt
                    | 0x00uy -> this.BRK Implied (apc this.Status 1) //Check if BRK advance the PC
                    //Comparisons - CMP, CPX, CPY
                    | 0xC9uy -> this.Compare (Immediate(gba 1)) (apc this.Status 2) this.Status.Accumulator 2
                    | 0xC5uy -> this.Compare (ZeroPage(gba 1)) (apc this.Status 2) this.Status.Accumulator 3
                    | 0xD5uy -> this.Compare (ZeroPageX(gba 1)) (apc this.Status 2) this.Status.Accumulator 4
                    | 0xCDuy -> this.Compare (Absolute(new Word(gba 2, gba 1))) (apc this.Status 3) this.Status.Accumulator 4
                    | 0xDDuy -> this.Compare (AbsoluteX(new Word(gba 2, gba 1))) (apc this.Status 3) this.Status.Accumulator 4
                    | 0xD9uy -> this.Compare (AbsoluteY(new Word(gba 2, gba 1))) (apc this.Status 3) this.Status.Accumulator 4
                    | 0xC1uy -> this.Compare (IndirectX(gba 1)) (apc this.Status 2) this.Status.Accumulator 6
                    | 0xD1uy -> this.Compare (IndirectY(gba 1)) (apc this.Status 2) this.Status.Accumulator 5
                    | 0xE0uy -> this.Compare (Immediate(gba 1)) (apc this.Status 2) this.Status.X 2
                    | 0xE4uy -> this.Compare (ZeroPage(gba 1)) (apc this.Status 2) this.Status.X 3
                    | 0xECuy -> this.Compare (Absolute(new Word(gba 2, gba 1))) (apc this.Status 2) this.Status.X 4
                    | 0xC0uy -> this.Compare (Immediate(gba 1)) (apc this.Status 2) this.Status.Y 2
                    | 0xC4uy -> this.Compare (ZeroPage(gba 1)) (apc this.Status 2) this.Status.Y 3
                    | 0xCCuy -> this.Compare (Absolute(new Word(gba 2, gba 1))) (apc this.Status 2) this.Status.Y 4
                    //LDA - Load Accumulator
                    | 0xA9uy -> this.LDA (Immediate(gba 1)) (apc this.Status 2) 2
                    | 0xA5uy -> this.LDA (ZeroPage(gba 1)) (apc this.Status 2) 3
                    | 0xB5uy -> this.LDA (ZeroPageX(gba 1)) (apc this.Status 2) 4
                    | 0xADuy -> this.LDA (Absolute(new Word(gba 2, gba 1))) (apc this.Status 3) 4
                    | 0xBDuy -> this.LDA (AbsoluteX(new Word(gba 2, gba 1))) (apc this.Status 3) 4
                    | 0xB9uy -> this.LDA (AbsoluteY(new Word(gba 2, gba 1))) (apc this.Status 3) 4
                    | 0xA1uy -> this.LDA (IndirectX(gba 1)) (apc this.Status 2) 6
                    | 0xB1uy -> this.LDA (IndirectY(gba 1)) (apc this.Status 2) 5
                    //LDX - Load X Register
                    | 0xA2uy -> this.LDX (Immediate(gba 1)) (apc this.Status 2) 2
                    | 0xA6uy -> this.LDX (ZeroPage(gba 1)) (apc this.Status 2) 3
                    | 0xB6uy -> this.LDX (ZeroPageY(gba 1)) (apc this.Status 2) 4
                    | 0xAEuy -> this.LDX (Absolute(new Word(gba 2, gba 1))) (apc this.Status 3) 4
                    | 0xBEuy -> this.LDX (AbsoluteY(new Word(gba 2, gba 1))) (apc this.Status 3) 4
                    //LDY - Load X Register
                    | 0xA0uy -> this.LDY (Immediate(gba 1)) (apc this.Status 2) 2
                    | 0xA4uy -> this.LDY (ZeroPage(gba 1)) (apc this.Status 2) 3
                    | 0xB4uy -> this.LDY (ZeroPageX(gba 1)) (apc this.Status 2) 4
                    | 0xACuy -> this.LDY (Absolute(new Word(gba 2, gba 1))) (apc this.Status 3) 4
                    | 0xBCuy -> this.LDY (AbsoluteX(new Word(gba 2, gba 1))) (apc this.Status 3) 4
                    //Store Instructions - STA, STX, STY
                    | 0x85uy -> this.Store (ZeroPage(gba 1)) (apc this.Status 2) this.Status.Accumulator 3
                    | 0x95uy -> this.Store (ZeroPageX(gba 1)) (apc this.Status 2) this.Status.Accumulator 4
                    | 0x8Duy -> this.Store (Absolute(new Word(gba 2, gba 1))) (apc this.Status 3) this.Status.Accumulator 4
                    | 0x9Duy -> this.Store (AbsoluteX(new Word(gba 2, gba 1))) (apc this.Status 3) this.Status.Accumulator 5
                    | 0x99uy -> this.Store (AbsoluteY(new Word(gba 2, gba 1))) (apc this.Status 3) this.Status.Accumulator 5
                    | 0x81uy -> this.Store (IndirectX(gba 1)) (apc this.Status 2) this.Status.Accumulator 6
                    | 0x91uy -> this.Store (IndirectY(gba 1)) (apc this.Status 2) this.Status.Accumulator 6
                    | 0x86uy -> this.Store (ZeroPage(gba 1)) (apc this.Status 2) this.Status.Accumulator 3
                    | 0x96uy -> this.Store (ZeroPageY(gba 1)) (apc this.Status 2) this.Status.Accumulator 4
                    | 0x8Euy -> this.Store (Absolute(new Word(gba 2, gba 1))) (apc this.Status 3) this.Status.Accumulator 4
                    | 0x84uy -> this.Store (ZeroPage(gba 1)) (apc this.Status 2) this.Status.Accumulator 3
                    | 0x94uy -> this.Store (ZeroPageX(gba 1)) (apc this.Status 2) this.Status.Accumulator 4
                    | 0x8Cuy -> this.Store (Absolute(new Word(gba 2, gba 1))) (apc this.Status 3) this.Status.Accumulator 4
                    //Not recognized...
                    | _ -> failwithf "Opcode '%s' not supported" (BitConverter.ToString [| opcode |])

        member private this.Branch opcode offset status =
            let jump =
                match opcode with
                | 0x10uy -> not status.Flags.Negative  //BPL
                | 0x30uy -> status.Flags.Negative      //BMI
                | 0x50uy -> not status.Flags.Overflow   //BVC
                | 0x70uy -> status.Flags.Overflow       //BVS
                | 0x90uy -> not status.Flags.Carry      //BCC
                | 0xB0uy -> status.Flags.Carry          //BCS
                | 0xD0uy -> not status.Flags.Zero       //BNE
                | 0xF0uy -> status.Flags.Zero           //BEQ
                | _ -> failwithf "'%s' is not a branching opcode" (BitConverter.ToString [| opcode |])

            let pc =
                if jump then
                    if offset < 0x80uy then status.ProgramCounter.Add(offset) //Jump forward
                    else status.ProgramCounter.Substract(0xFFuy - offset + 1uy) //Jump backward
                else status.ProgramCounter //Don't jump, you fool!
            {
                (ac status (2 + (if jump then 1 else 0)) (pc.Msb > status.ProgramCounter.Msb)) with
                    ProgramCounter = pc
            }

        member private this.Flags opcode status =
            match opcode with
            | 0x18uy -> { (ac status 2 false) with Flags = { status.Flags with Carry = false } }       //CLC
            | 0x38uy -> { (ac status 2 false) with Flags = { status.Flags with Carry = true } }        //SEC
            | 0x58uy -> { (ac status 2 false) with Flags = { status.Flags with Interrupt = false } }   //CLI
            | 0x78uy -> { (ac status 2 false) with Flags = { status.Flags with Interrupt = true } }    //SEI
            | 0xB8uy -> { (ac status 2 false) with Flags = { status.Flags with Overflow = false } }    //CLV
            | 0xD8uy -> { (ac status 2 false) with Flags = { status.Flags with Decimal = false } }     //CLD
            | 0xF8uy -> { (ac status 2 false) with Flags = { status.Flags with Decimal = true } }      //SED
            | _ -> failwithf "'%s' is not a flag manipulation opcode" (BitConverter.ToString [| opcode |])

        member private this.Registers opcode status =
            let setFlags value flags = { flags with Zero = value = 0x00uy; Negative = value >= 0x80uy }

            match opcode with
            | 0xAAuy -> { (ac status 2 false) with X = status.Accumulator }                                  //TAX
            | 0x8Auy -> { (ac status 2 false) with Accumulator = status.X }                                  //TXA
            | 0xCAuy ->                                                                                      //DEX
                let newX = status.X - 01uy
                { (ac status 2 false) with X = newX; Flags = setFlags newX status.Flags }
            | 0xE8uy ->                                                                                      //INX
                let newX = status.X + 01uy
                { (ac status 2 false) with X = newX; Flags = setFlags newX status.Flags }
            | 0xA8uy -> { (ac status 2 false) with Y = status.Accumulator }                                  //TAY
            | 0x98uy -> { (ac status 2 false) with Accumulator = status.Y }                                  //TYA
            | 0x88uy ->                                                                                      //DEY
                let newY = status.Y - 01uy
                { (ac status 2 false) with Y = newY; Flags = setFlags newY status.Flags }
            | 0xC8uy ->                                                                                      //IN
                let newY = status.Y + 01uy
                { (ac status 2 false) with Y = newY; Flags = setFlags newY status.Flags }
            | 0x9Auy -> { (ac status 2 false) with StackPointer = new Word(StackPageReference, status.X) }   //TXS
            | 0xBAuy -> { (ac status 2 false) with X = status.StackPointer.Lsb }                             //TSX
            | _ -> failwithf "'%s' is not a register manipulation opcode" (BitConverter.ToString [| opcode |])

        member private this.Stack opcode status =
            let push b status =
                status.Memory.[status.StackPointer] <- b
                { status with StackPointer = status.StackPointer.Substract 1uy }
            let pop status =
                let newSP = status.StackPointer.Add 1uy
                let b = status.Memory.[newSP]
                (b, { status with StackPointer = newSP })

            match opcode with
            | 0x48uy -> push status.Accumulator (ac status 3 false)                                                       //PHA
            | 0x68uy ->                                                                                                   //PLA
                let (b, newStatus) = pop status
                { (ac newStatus 4 false) with Accumulator = b }
            | 0x08uy ->                                                                                                   //PHP
                let mutable ps = 0uy
                if status.Flags.Carry then ps <- ps ||| (1uy <<< 0)
                if status.Flags.Zero then ps <- ps ||| (1uy <<< 1)
                if status.Flags.Interrupt then ps <- ps ||| (1uy <<< 2)
                if status.Flags.Decimal then ps <- ps ||| (1uy <<< 3)
                if status.Flags.BreakCommand then ps <- ps ||| (1uy <<< 4)
                if status.Flags._Reserved then ps <- ps ||| (1uy <<< 5)
                if status.Flags.Overflow then ps <- ps ||| (1uy <<< 6)
                if status.Flags.Negative then ps <- ps ||| (1uy <<< 7)
                ac (push ps status) 3 false
            | 0x28uy ->                                                                                                   //PLP
                let (b, newStatus) = pop status
                ac ({ newStatus with
                        Flags =
                            {
                                Carry = (b &&& (1uy <<< 0) > 0uy);
                                Zero = (b &&& (1uy <<< 1) > 0uy);
                                Interrupt = (b &&& (1uy <<< 2) > 0uy);
                                Decimal = (b &&& (1uy <<< 3) > 0uy);
                                BreakCommand = (b &&& (1uy <<< 4) > 0uy);
                                _Reserved = (b &&& (1uy <<< 5) > 0uy);
                                Overflow = (b &&& (1uy <<< 6) > 0uy);
                                Negative = (b &&& (1uy <<< 7) > 0uy);
                            }
                }) 4 false
            | _ -> failwithf "'%s' is not a stack manipulation opcode" (BitConverter.ToString [| opcode |])

        member private this.ADC mode status cycles =
            let (value, pageCrossed) = gv mode status

            if not (status.Flags.Decimal) then
                let result = status.Accumulator + value + (if status.Flags.Carry then 1uy else 0uy)
                let carry = (result < status.Accumulator && result < value)
                let overflow = if carry then status.Accumulator >= 0x80uy && value >= 0x80uy && result < 0x80uy else status.Accumulator < 0x80uy && value < 0x80uy && result >= 0x80uy
                { (ac status cycles pageCrossed) with Accumulator = result; Flags = { status.Flags with Zero = result = 0uy; Carry = carry; Overflow = overflow } }
            else
                //Do Something else
                status
//
//
//        member private this.AND mode status =
//            match mode with
//            | AddressingModes.Immediate(arg) ->
//                //TODO Implement operation
//                ac status 2
//            | _ -> failwith "Not supported"

        member private this.BRK mode status =
            match mode with
            | AddressingModes.Implied ->
                {
                    (ac status 7 false) with
                        Flags =
                            {
                                status.Flags with BreakCommand = true
                            }
                }
            | _ -> failwith "Not supported"

        member private this.Compare mode status value cycles =
            let (m, pageCrossed) = gv mode status
            { (ac status cycles pageCrossed) with Flags = { status.Flags with Zero = value = m; Carry = value >= m; Negative = value >= 0x80uy } }

        member private this.LDA mode status cycles =
            let (value, pageCrossed) = gv mode status
            { (ac status cycles pageCrossed) with Accumulator = value; Flags = { status.Flags with Zero = value = 0uy; Negative = value >= 0x80uy } }

        member private this.LDX mode status  cycles =
            let (value, pageCrossed) = gv mode status
            { (ac status cycles pageCrossed) with X = value; Flags = { status.Flags with Zero = value = 0uy; Negative = value >= 0x80uy } }

        member private this.LDY mode status cycles =
            let (value, pageCrossed) = gv mode status
            { (ac status cycles pageCrossed) with Y = value; Flags = { status.Flags with Zero = value = 0uy; Negative = value >= 0x80uy } }

        member private this.Store mode status value cycles =
            let (address, _) = ga mode status //For these instructions we don't care if page were crossed of not
            status.Memory.[address] <- value
            ac status cycles false