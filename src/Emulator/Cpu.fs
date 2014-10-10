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
            IndexX: byte
            IndexY: byte
        }

    type Flags =
        {
            Carry: bool
            Zero: bool
            Interrupt: bool
            Decimal: bool
            SoftwareInterrupt: bool
            _Reserved: bool
            Overflow: bool
            Sign: bool
        }

    type Memory() =
        member private this.memory = Array.init 256 (fun i -> Array.create 256 0uy)

        member this.Item
            with get(address: (byte * byte)) = this.memory.[System.BitConverter.ToInt32([|fst address|], 0)].[System.BitConverter.ToInt32([|snd address|], 0)]
            and internal set(address: (byte * byte)) value = this.memory.[System.BitConverter.ToInt32([|fst address|], 0)].[System.BitConverter.ToInt32([|snd address|], 0)] <- value

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
        let mutable status =
            {
                Registers =
                    {
                        Accumulator = 0uy
                        IndexX = 0uy
                        IndexY = 0uy
                    }
                Flags =
                    {
                        Carry = false
                        Zero = false
                        Interrupt = false
                        Decimal = false
                        SoftwareInterrupt = true
                        _Reserved = true
                        Overflow = false
                        Sign = false
                    }
                Memory = new Memory()
                ProgramCounter = 0us
                StackPointer = 0us
                Cycles = 0UL
            }

        member this.Status
            with public get() = status
            and private set(value) = status <- value
        
        member this.Execute (program: byte[]) =
            let mutable pc = this.Status.ProgramCounter

            let getNextByte =
                pc <- pc + 1us //Increment PC each time we fetch a byte somewhere
                program.[Convert.ToInt32 (pc - 1us)]

            //Main loop
            while this.Status.ProgramCounter < Convert.ToUInt16(program.Length) do
                let opcode = getNextByte //Parse the next byte to get the opcode as a string

                //Change in the cpu's status is atomic and no transitional state should be externally observable
                let newStatus =
                    match opcode with
                    //ADC - Add with Carry
                    | 0x69uy -> this.ADC (AddressingModes.Immediate(getNextByte))
                    | 0x65uy -> this.ADC (AddressingModes.ZeroPage(getNextByte))
                    | 0x75uy -> this.ADC (AddressingModes.ZeroPageX(getNextByte))
                    | 0x6Duy -> this.ADC (AddressingModes.Absolute(getNextByte, getNextByte))
                    | 0x7Duy -> this.ADC (AddressingModes.AbsoluteX(getNextByte, getNextByte))
                    | 0x79uy -> this.ADC (AddressingModes.AbsoluteY(getNextByte, getNextByte))
                    | 0x61uy -> this.ADC (AddressingModes.IndirectX(getNextByte))
                    | 0x71uy -> this.ADC (AddressingModes.IndirectY(getNextByte))
                    //AND - Bitwise AND with Accumulator
                    | 0x29uy -> this.AND (AddressingModes.Immediate(getNextByte))
                    //LDA - Load Accumulator
                    | 0xA9uy -> this.LDA (AddressingModes.Immediate(getNextByte))
                    //STA - Store Accumulator
                    | 0x8Duy -> this.STA (AddressingModes.Absolute(getNextByte, getNextByte))
                    | _ -> failwith "Opcode not supported"

                //Finally update the status with the value of the local pc
                this.Status <- { newStatus with ProgramCounter = pc }

        member private this.ADC mode =
            match mode with
            | AddressingModes.Immediate(arg) ->
                //TODO Implement operation
                { this.Status with Cycles = this.Status.Cycles + 2UL }
            | _ -> failwith "Not supported"

        member private this.AND mode =
            match mode with
            | AddressingModes.Immediate(arg) ->
                //TODO Implement operation
                { this.Status with Cycles = this.Status.Cycles + 2UL }
            | _ -> failwith "Not supported"

        member private this.LDA mode =
            match mode with
            | AddressingModes.Immediate(arg) ->
                { this.Status with Cycles = this.Status.Cycles + 2UL; Registers = { this.Status.Registers with Accumulator = arg } }
            | _ -> failwith "Not supported"

        member private this.STA mode =
            match mode with
            | AddressingModes.Absolute(arg0, arg1) ->
                //TODO Find a way to "functionnaly" modify the memory in an atomic way with the reste of the status, if possible
                this.Status.Memory.[(arg0, arg1)] <- this.Status.Registers.Accumulator
                { this.Status with Cycles = this.Status.Cycles + 4UL }
            | _ -> failwith "Not supported"