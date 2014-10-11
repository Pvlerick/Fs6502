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

    type Registers =
        {
            Accumulator: byte
            X: byte
            Y: byte
        }

    type Flags =
        {
            Carry: bool                 // C
            Zero: bool                  // Z
            Interrupt: bool             // I
            Decimal: bool               // D
            BreakCommand: bool          //B
            _Reserved: bool             // -
            Overflow: bool              // V
            Negavtive: bool             // N
        }

    type Memory() =
        let toInt b = System.BitConverter.ToInt32([| b; 0x00uy; 0x00uy; 0x00uy |], 0)

        member private this.memory = Array.init 256 (fun i -> Array.create 256 0uy)

        member this.Item
            with get(address: (byte * byte)) = this.memory.[System.BitConverter.ToInt32([|fst address|], 0)].[System.BitConverter.ToInt32([|snd address|], 0)]
            and internal set(address: (byte * byte)) value = this.memory.[toInt (fst address)].[toInt (snd address)] <- value

    type Status =
        {
            Registers: Registers
            Flags: Flags
            Memory: Memory
            ProgramCounter: UInt16
            StackPointer: UInt16
            Cycles: UInt64
        }

    type Cpu() =
        //Advancing PC and adding cycles are so common operation that it's handy to have functions to do it
        let advancePC (status: Status) (count: int) = { status with ProgramCounter = status.ProgramCounter + Convert.ToUInt16(count) }
        let addCycles (status: Status) (count: int) = { status with Cycles = status.Cycles + Convert.ToUInt64(count) }

        let toUInt b = System.BitConverter.ToUInt16([| b; 0x00uy |], 0)

        member val public Status =
            {
                Registers =
                    {
                        Accumulator = 0uy
                        X = 0uy
                        Y = 0uy
                    }
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
                Memory = new Memory()
                ProgramCounter = 0us
                StackPointer = 0us
                Cycles = 0UL
            }
            with get, set

        member this.Execute startingAddress (machineCode: byte[]) =
            let getByteAhead (offset: int) = machineCode.[Convert.ToInt32 (this.Status.ProgramCounter - startingAddress + Convert.ToUInt16(offset))]
            
            this.Status <- advancePC this.Status (Convert.ToInt32 startingAddress)

            //Main loop
            while this.Status.ProgramCounter < startingAddress + Convert.ToUInt16(machineCode.Length) do
                let opcode = getByteAhead 0 //Parse the next byte to get the opcode as a string

                //Change in the cpu's status is atomic and no transitional state should be externally observable
                this.Status <-
                    match opcode with
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
                    //Branches - BPL, BMI, BVC, BVC, BCC, BCS, BNE, BEQ
                    | 0x10uy | 0x30uy | 0x50uy | 0x70uy | 0x90uy | 0xB0uy | 0xD0uy | 0xF0uy ->
                        this.Branch opcode (AddressingModes.Relative(getByteAhead 1)) (advancePC this.Status 2)
                    //BRK - Force Interrupt
                    | 0x00uy -> this.BRK (AddressingModes.Implied) (advancePC this.Status 1) //Check if BRK advance the PC
                    //CPX - Compare X Register
                    | 0xE0uy -> this.CPX (AddressingModes.Immediate(getByteAhead 1)) (advancePC this.Status 2)
                    //DEX - Decrement X Register
                    | 0xCAuy -> this.DEX (AddressingModes.Implied) (advancePC this.Status 1)
                    //LDA - Load Accumulator
                    | 0xA9uy -> this.LDA (AddressingModes.Immediate(getByteAhead 1)) (advancePC this.Status 2)
                    //LDX - Load X Register
                    | 0xA2uy -> this.LDX (AddressingModes.Immediate(getByteAhead 1)) (advancePC this.Status 2)
                    //STA - Store Accumulator
                    | 0x8Duy -> this.STA (AddressingModes.Absolute(getByteAhead 2, getByteAhead 1)) (advancePC this.Status 3)
                    //STX - Store X Register
                    | 0x8Euy -> this.STX (AddressingModes.Absolute(getByteAhead 2, getByteAhead 1)) (advancePC this.Status 3)
                    | _ -> failwithf "Opcode '%s' not supported" (BitConverter.ToString [| opcode |])

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

        member private this.Branch opcode mode status =
            let jump =
                match opcode with
                | 0x10uy -> not status.Flags.Negavtive  //BPL
                | 0x30uy -> status.Flags.Negavtive      //BMI
                | 0x50uy -> not status.Flags.Overflow   //BVC
                | 0x70uy -> status.Flags.Overflow       //BVS
                | 0x90uy -> not status.Flags.Carry      //BCC
                | 0xB0uy -> status.Flags.Carry          //BCS
                | 0xD0uy -> not status.Flags.Zero       //BNE
                | 0xF0uy -> status.Flags.Zero           //BEQ
                | _ -> failwith "Not a branching opcode"

            match mode with
            | AddressingModes.Relative(arg) ->
                let pc =
                    if jump then (if arg < 0x80uy then status.ProgramCounter + (toUInt arg) else status.ProgramCounter - ((toUInt (0xFFuy - arg)) + 1us))
                    else status.ProgramCounter
                {
                    (addCycles status (2 + (if jump then 1 else 0))) with //TODO if change of page +2
                        ProgramCounter = pc
                }
            | _ -> failwith "Not supported"

        member private this.BRK mode status =
            match mode with
            | AddressingModes.Implied ->
                {
                    (addCycles status 7) with
                        Flags =
                            {
                                status.Flags with BreakCommand = true
                            }
                }
            | _ -> failwith "Not supported"

        member private this.CPX mode status =
            match mode with
            | AddressingModes.Immediate(arg) ->
                {
                    (addCycles status 4) with
                        Flags =
                            {
                                status.Flags with
                                    Zero = status.Registers.X = arg
                                    Carry = status.Registers.X >= arg
                                    Negavtive = status.Registers.X >= 0x80uy
                            }
                }
            | _ -> failwith "Not supported"

        member private this.DEX mode status =
            match mode with
            | AddressingModes.Implied ->
                let newX = status.Registers.X - 0x01uy
                {
                    (addCycles status 2) with
                        Registers = { status.Registers with X = newX };
                        Flags = { status.Flags with Zero = (newX = 0x00uy); Negavtive = (newX > 0x7Fuy) }
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
                        Registers = { status.Registers with X = arg };
                        Flags = { status.Flags with Zero = (arg = 0x00uy); Negavtive = (arg > 0x7Fuy) }
                }
            | _ -> failwith "Not supported"

        member private this.STA mode status =
            match mode with
            | AddressingModes.Absolute(arg0, arg1) ->
                //TODO Find a way to "functionnaly" modify the memory in an atomic way with the reste of the status, if possible
                status.Memory.[(arg0, arg1)] <- status.Registers.Accumulator
                addCycles status 4
            | _ -> failwith "Not supported"

        member private this.STX mode status =
            match mode with
            | AddressingModes.Absolute(arg0, arg1) ->
                //TODO Find a way to "functionnaly" modify the memory in an atomic way with the reste of the status, if possible
                status.Memory.[(arg0, arg1)] <- status.Registers.X
                addCycles status 4
            | _ -> failwith "Not supported"