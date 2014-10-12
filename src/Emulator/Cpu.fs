namespace Emulator
    open System

    type private AddressingModes =
        | Implied
        | Immediate of byte
        | ZeroPage of byte
        | ZeroPageX of byte
        | ZeroPageY of byte
        | Absolute of byte * byte
        | AbsoluteX of byte * byte
        | AbsoluteY of byte * byte
        | Indirect of byte * byte
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
            Negavtive: bool             // N
        }

    type Registers =
        {
            Accumulator: byte
            X: byte
            Y: byte
            StackPointer: byte
            ProgramCounter: UInt16
            Flags: Flags
        }

    type Memory() =
        let byteToInt b = System.BitConverter.ToInt32([| b; 0x00uy; 0x00uy; 0x00uy |], 0)

        let memory = Array2D.init 256 256 (fun x y -> 0uy)

        member this.Item
            with get(address: (byte * byte)) = memory.[byteToInt (fst address), byteToInt (snd address)]
            and internal set(address: (byte * byte)) value = memory.[byteToInt (fst address), byteToInt (snd address)] <- value

    type Status =
        {
            Registers: Registers
            Memory: Memory
            Cycles: UInt64
        }

    type Cpu() =
        [<Literal>] 
        let StartingStackPointer = 0xFFuy

        //Advancing PC and adding cycles are so common operation that it's handy to have functions to do it
        let advancePC (status: Status) (count: int) = { status with Registers = { status.Registers with ProgramCounter = status.Registers.ProgramCounter + Convert.ToUInt16(count) } }
        let addCycles (status: Status) (count: int) = { status with Cycles = status.Cycles + Convert.ToUInt64(count) }

        //Misc conversions functions: byte(s) to uint16, add byte to (byte * byte)...
        let byteToUInt b = System.BitConverter.ToUInt16([| b; 0uy |], 0)
        let bytesToUInt (b0, b1) = System.BitConverter.ToUInt16([| b1; b0 |], 0)
        let addByteToWord (w: (byte * byte)) b =
            let (arg0, arg1) = w
            let bs = BitConverter.GetBytes(BitConverter.ToUInt16([| arg0; arg1 |], 0) + (byteToUInt b))
            (bs.[0], bs.[1])

        member val public Status =
            {
                Registers =
                    {
                        Accumulator = 0uy
                        X = 0uy
                        Y = 0uy
                        ProgramCounter = 0us
                        StackPointer = StartingStackPointer
                        Flags =
                            {
                                Carry = false
                                Zero = false
                                Interrupt = false
                                Decimal = false
                                BreakCommand = true
                                _Reserved = true
                                Overflow = false
                                Negavtive = false
                            }
                    }
                Memory = new Memory()
                Cycles = 0UL
            }
            with get, set

        member this.Execute startingAddress (machineCode: byte[]) =
            let getByteAhead (offset: int) = machineCode.[Convert.ToInt32 (this.Status.Registers.ProgramCounter - startingAddress + Convert.ToUInt16(offset))]
            
            this.Status <- advancePC this.Status (Convert.ToInt32 startingAddress)

            //Main loop
            while this.Status.Registers.ProgramCounter < startingAddress + Convert.ToUInt16(machineCode.Length) do
                let opcode = getByteAhead 0 //Parse the next byte to get the opcode as a string

                //Change in the cpu's status is atomic and no transitional state should be externally observable
                this.Status <-
                    match opcode with
                    //Branches - BPL, BMI, BVC, BVC, BCC, BCS, BNE, BEQ
                    | 0x10uy | 0x30uy | 0x50uy | 0x70uy | 0x90uy | 0xB0uy | 0xD0uy | 0xF0uy ->
                        this.Branch opcode (getByteAhead 1) (advancePC this.Status 2)
                    //Flags manipulations
                    | 0x18uy | 0x38uy | 0x58uy | 0x78uy | 0xB8uy | 0xD8uy | 0xF8uy ->
                        this.Flags opcode (advancePC this.Status 1)
                    //Registers manipulations
                    | 0xAAuy | 0x8Auy | 0xCAuy | 0xE8uy | 0xA8uy | 0x98uy | 0x88uy | 0xC8uy | 0x9Auy | 0xBAuy ->
                        this.Registers opcode (advancePC this.Status 1)
                    //Stack manipulations
                    | 0x48uy | 0x68uy | 0x08uy | 0x28uy ->
                        this.Stack opcode (advancePC this.Status 1)
                    //ADC - Add with Carry
                    | 0x69uy -> this.ADC (AddressingModes.Immediate(getByteAhead 1)) (advancePC this.Status 2)
                    | 0x65uy -> this.ADC (AddressingModes.ZeroPage(getByteAhead 1)) (advancePC this.Status 2)
                    | 0x75uy -> this.ADC (AddressingModes.ZeroPageX(getByteAhead 1)) (advancePC this.Status 2)
                    | 0x6Duy -> this.ADC (AddressingModes.Absolute(getByteAhead 2, getByteAhead 1)) (advancePC this.Status 3)
                    | 0x7Duy -> this.ADC (AddressingModes.AbsoluteX(getByteAhead 2, getByteAhead 1)) (advancePC this.Status 3)
                    | 0x79uy -> this.ADC (AddressingModes.AbsoluteY(getByteAhead 2, getByteAhead 1)) (advancePC this.Status 3)
                    | 0x61uy -> this.ADC (AddressingModes.IndirectX(getByteAhead 1)) (advancePC this.Status 2)
                    | 0x71uy -> this.ADC (AddressingModes.IndirectY(getByteAhead 1)) (advancePC this.Status 2)
                    //AND - Bitwise AND with Accumulator
                    | 0x29uy -> this.AND (AddressingModes.Immediate(getByteAhead 1)) (advancePC this.Status 2)
                    //BRK - Force Interrupt
                    | 0x00uy -> this.BRK (AddressingModes.Implied) (advancePC this.Status 1) //Check if BRK advance the PC
                    //CPX - Compare X Register
                    | 0xE0uy -> this.CPX (AddressingModes.Immediate(getByteAhead 1)) (advancePC this.Status 2)
                    //CPY - Compare Y Register
                    | 0xC0uy -> this.CPY (AddressingModes.Immediate(getByteAhead 1)) (advancePC this.Status 2)
                    //LDA - Load Accumulator
                    | 0xA9uy -> this.LDA (AddressingModes.Immediate(getByteAhead 1)) (advancePC this.Status 2)
                    //LDX - Load X Register
                    | 0xA2uy -> this.LDX (AddressingModes.Immediate(getByteAhead 1)) (advancePC this.Status 2)
                    //LDY - Load X Register
                    | 0xA0uy -> this.LDY (AddressingModes.Immediate(getByteAhead 1)) (advancePC this.Status 2)
                    //STA - Store Accumulator
                    | 0x8Duy -> this.STA (AddressingModes.Absolute(getByteAhead 2, getByteAhead 1)) (advancePC this.Status 3)
                    | 0x99uy -> this.STA (AddressingModes.AbsoluteY(getByteAhead 2, getByteAhead 1)) (advancePC this.Status 3)
                    //STX - Store X Register
                    | 0x8Euy -> this.STX (AddressingModes.Absolute(getByteAhead 2, getByteAhead 1)) (advancePC this.Status 3)
                    | _ -> failwithf "Opcode '%s' not supported" (BitConverter.ToString [| opcode |])

        member private this.Branch opcode offset status =
            let jump =
                match opcode with
                | 0x10uy -> not status.Registers.Flags.Negavtive  //BPL
                | 0x30uy -> status.Registers.Flags.Negavtive      //BMI
                | 0x50uy -> not status.Registers.Flags.Overflow   //BVC
                | 0x70uy -> status.Registers.Flags.Overflow       //BVS
                | 0x90uy -> not status.Registers.Flags.Carry      //BCC
                | 0xB0uy -> status.Registers.Flags.Carry          //BCS
                | 0xD0uy -> not status.Registers.Flags.Zero       //BNE
                | 0xF0uy -> status.Registers.Flags.Zero           //BEQ
                | _ -> failwithf "'%s' is not a branching opcode" (BitConverter.ToString [| opcode |])

            let pc =
                if jump then (if offset < 0x80uy then status.Registers.ProgramCounter + (byteToUInt offset) else status.Registers.ProgramCounter - ((byteToUInt (0xFFuy - offset)) + 1us))
                else status.Registers.ProgramCounter
            {
                (addCycles status (2 + (if jump then 1 else 0))) with //TODO if change of page +2
                    Registers = { status.Registers with ProgramCounter = pc }
            }

        member private this.Flags opcode status =
            match opcode with
            | 0x18uy -> { (addCycles status 2) with Registers = { status.Registers with Flags = { status.Registers.Flags with Carry = false } } }       //CLC
            | 0x38uy -> { (addCycles status 2) with Registers = { status.Registers with Flags = { status.Registers.Flags with Carry = true } } }        //SEC
            | 0x58uy -> { (addCycles status 2) with Registers = { status.Registers with Flags = { status.Registers.Flags with Interrupt = false } } }   //CLI
            | 0x78uy -> { (addCycles status 2) with Registers = { status.Registers with Flags = { status.Registers.Flags with Interrupt = true } } }    //SEI
            | 0xB8uy -> { (addCycles status 2) with Registers = { status.Registers with Flags = { status.Registers.Flags with Overflow = false } } }    //CLV
            | 0xD8uy -> { (addCycles status 2) with Registers = { status.Registers with Flags = { status.Registers.Flags with Decimal = false } } }     //CLD
            | 0xF8uy -> { (addCycles status 2) with Registers = { status.Registers with Flags = { status.Registers.Flags with Decimal = true } } }      //SED
            | _ -> failwithf "'%s' is not a flag manipulation opcode" (BitConverter.ToString [| opcode |])

        member private this.Registers opcode status =
            let setFlags value flags = { flags with Zero = (value = 0x00uy); Negavtive = (value > 0x7Fuy) }

            match opcode with
            | 0xAAuy -> { status with Registers = { status.Registers with X = status.Registers.Accumulator } }                      //TAX
            | 0x8Auy -> { status with Registers = { status.Registers with Accumulator = status.Registers.X } }                      //TXA
            | 0xCAuy ->                                                                                                             //DEX
                let newX = status.Registers.X - 01uy
                { (addCycles status 2) with Registers = { status.Registers with X = newX; Flags = setFlags newX status.Registers.Flags } }
            | 0xE8uy ->                                                                                                             //INX
                let newX = status.Registers.X + 01uy
                { (addCycles status 2) with Registers = { status.Registers with X = newX; Flags = setFlags newX status.Registers.Flags } }
            | 0xA8uy -> { status with Registers = { status.Registers with Y = status.Registers.Accumulator } }                      //TAY
            | 0x98uy -> { status with Registers = { status.Registers with Accumulator = status.Registers.Y } }                      //TYA
            | 0x88uy ->                                                                                                             //DEX
                let newY = status.Registers.Y - 01uy
                { (addCycles status 2) with Registers = { status.Registers with Y = newY; Flags = setFlags newY status.Registers.Flags } }
            | 0xC8uy ->                                                                                                             //INX
                let newY = status.Registers.Y + 01uy
                { (addCycles status 2) with Registers = { status.Registers with Y = newY; Flags = setFlags newY status.Registers.Flags } }
            | 0x9Auy -> { status with Registers = { status.Registers with StackPointer = status.Registers.X } }                     //TXS
            | 0xBAuy -> { status with Registers = { status.Registers with X = status.Registers.StackPointer } }                     //TSX
            | _ -> failwithf "'%s' is not a register manipulation opcode" (BitConverter.ToString [| opcode |])

        member private this.Stack opcode status =
            let push b status =
                status.Memory.[(1uy, status.Registers.StackPointer)] <- b
                { status with Registers = { status.Registers with StackPointer = status.Registers.StackPointer - 1uy } }
            let pop status =
                let b = status.Memory.[(1uy, status.Registers.StackPointer + 1uy)]
                (b, { status with Registers = { status.Registers with StackPointer = status.Registers.StackPointer + 1uy } })

            match opcode with
            | 0x48uy -> push status.Registers.Accumulator (addCycles status 3)                                                      //PHA
            | 0x68uy ->                                                                                                             //PLA
                let (b, newStatus) = pop status
                { (addCycles newStatus 4) with Registers = { newStatus.Registers with Accumulator = b } }
            | 0x08uy ->                                                                                                             //PHP
                let mutable ps = 0uy
                if status.Registers.Flags.Carry then ps <- ps ||| (1uy <<< 0)
                if status.Registers.Flags.Zero then ps <- ps ||| (1uy <<< 1)
                if status.Registers.Flags.Interrupt then ps <- ps ||| (1uy <<< 2)
                if status.Registers.Flags.Decimal then ps <- ps ||| (1uy <<< 3)
                if status.Registers.Flags.BreakCommand then ps <- ps ||| (1uy <<< 4)
                if status.Registers.Flags._Reserved then ps <- ps ||| (1uy <<< 5)
                if status.Registers.Flags.Overflow then ps <- ps ||| (1uy <<< 6)
                if status.Registers.Flags.Negavtive then ps <- ps ||| (1uy <<< 7)
                push ps status
            | 0x28uy ->                                                                                                             //PLP
                let (b, newStatus) = pop status
                {
                    newStatus with
                        Registers =
                            {
                                newStatus.Registers with                                   
                                    Flags =
                                        {
                                            Carry = (b &&& (1uy <<< 0) > 0uy);
                                            Zero = (b &&& (1uy <<< 1) > 0uy);
                                            Interrupt = (b &&& (1uy <<< 2) > 0uy);
                                            Decimal = (b &&& (1uy <<< 3) > 0uy);
                                            BreakCommand = (b &&& (1uy <<< 4) > 0uy);
                                            _Reserved = (b &&& (1uy <<< 5) > 0uy);
                                            Overflow = (b &&& (1uy <<< 6) > 0uy);
                                            Negavtive = (b &&& (1uy <<< 7) > 0uy);
                                        }
                            }
                }
            | _ -> failwithf "'%s' is not a stack manipulation opcode" (BitConverter.ToString [| opcode |])

        member private this.ADC mode status =
            match mode with
            | AddressingModes.Immediate(arg) ->
                //TODO Implement operation
                addCycles status 2
            | _ -> failwith "Not supported"

        member private this.AND mode status =
            match mode with
            | AddressingModes.Immediate(arg) ->
                //TODO Implement operation
                addCycles status 2
            | _ -> failwith "Not supported"

        member private this.BRK mode status =
            match mode with
            | AddressingModes.Implied ->
                {
                    (addCycles status 7) with
                        Registers =
                            {
                                status.Registers with
                                    Flags =
                                        {
                                            status.Registers.Flags with BreakCommand = true
                                        }
                            }
                }
            | _ -> failwith "Not supported"

        member private this.CPX mode status =
            match mode with
            | AddressingModes.Immediate(arg) ->
                {
                    (addCycles status 4) with
                        Registers =
                            {
                                status.Registers with
                                    Flags =
                                        {
                                            status.Registers.Flags with
                                                Zero = status.Registers.X = arg
                                                Carry = status.Registers.X >= arg
                                                Negavtive = status.Registers.X >= 0x80uy
                                        }
                            }
                }
            | _ -> failwith "Not supported"

        member private this.CPY mode status =
            match mode with
            | AddressingModes.Immediate(arg) ->
                {
                    (addCycles status 4) with
                        Registers =
                            {
                                status.Registers with
                                    Flags =
                                        {
                                            status.Registers.Flags with
                                                Zero = status.Registers.Y = arg
                                                Carry = status.Registers.Y >= arg
                                                Negavtive = status.Registers.Y >= 0x80uy
                                        }
                            }
                }
            | _ -> failwith "Not supported"

        member private this.LDA mode status =
            match mode with
            | AddressingModes.Immediate(arg) ->
                { (addCycles status 2) with Registers = { status.Registers with Accumulator = arg } }
            | _ -> failwith "Not supported"

        member private this.LDX mode status =
            match mode with
            | AddressingModes.Immediate(arg) ->
                {
                    (addCycles status 2) with
                        Registers =
                            {
                                status.Registers with
                                    X = arg;
                                    Flags = { status.Registers.Flags with Zero = (arg = 0x00uy); Negavtive = (arg > 0x7Fuy) }
                            }
                }
            | _ -> failwith "Not supported"

        member private this.LDY mode status =
            match mode with
            | AddressingModes.Immediate(arg) ->
                {
                    (addCycles status 2) with
                        Registers =
                            {
                                status.Registers with
                                    Y = arg;
                                    Flags = { status.Registers.Flags with Zero = (arg = 0x00uy); Negavtive = (arg > 0x7Fuy) }
                            }
                }
            | _ -> failwith "Not supported"

        member private this.STA mode status =
            match mode with
            | AddressingModes.Absolute(arg0, arg1) ->
                //TODO Find a way to "functionnaly" modify the memory in an atomic way with the reste of the status, if possible
                status.Memory.[(arg0, arg1)] <- status.Registers.Accumulator
                addCycles status 4
            | AddressingModes.AbsoluteY(arg0, arg1) ->
                let address = addByteToWord (arg1, arg0) status.Registers.Y
                status.Memory.[address] <- status.Registers.Accumulator
                addCycles status 5
            | _ -> failwith "Not supported"

        member private this.STX mode status =
            match mode with
            | AddressingModes.Absolute(arg0, arg1) ->
                //TODO Find a way to "functionnaly" modify the memory in an atomic way with the reste of the status, if possible
                status.Memory.[(arg0, arg1)] <- status.Registers.X
                addCycles status 4
            | _ -> failwith "Not supported"