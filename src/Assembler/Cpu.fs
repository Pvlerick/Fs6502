module Emulator

    type Registers() =
        member val public Accumulator = 0uy with get, set
        member val public IndexX = 0uy with get, set
        member val public IndexY = 0uy with get, set

    type Flags() =
        member private this.status = [| false; false; true; false; false; false; false; false |]

        member this.Carry
            with public get() = this.status.[7]
            and internal set(value) = this.status.[7] <- value
        member this.Zero
            with public get() = this.status.[6]
            and internal set(value) = this.status.[6] <- value
        member this.Interrupt
            with public get() = this.status.[5]
            and internal set(value) = this.status.[5] <- value
        member this.Decimal
            with public get() = this.status.[4]
            and internal set(value) = this.status.[4] <- value
        member this.SoftwareInterrupt
            with public get() = this.status.[2]
            and internal set(value) = this.status.[2] <- value
        member this.Overflow
            with public get() = this.status.[1]
            and internal set(value) = this.status.[1] <- value
        member this.Sign
            with public get() = this.status.[0]
            and internal set(value) = this.status.[0] <- value

    type Memory() =
        member private this.memory = Array.create (256 * 256) 0uy

        member this.Item
            with get(address: byte) = this.memory.[System.BitConverter.ToInt32([|address|], 0)]
            and internal set(address: byte) value = this.memory.[System.BitConverter.ToInt32([|address|], 0)] <- value

    type Cpu() =
        let mutable cycles = 0

        member public this.Cycles = cycles
        member private this.AddCycles c = cycles <- cycles + c

        member this.Registers = new Registers()
        member this.Flags = new Flags()
        member this.Memory = new Memory()