open Assembler

[<EntryPoint>]
let main argv = 
    //printfn "%A" argv.[0]

    let asm = new Assembler()

    let program =
        [
            "  LDX #$08"
            "decrement:"
            "  DEX"
            "  STX $0200"
            "  CPX #$03"
            "  BNE decrement"
            "  STX $0201"
            "  STX $0201"
            "  BRK"
        ]

    let assembly = asm.Assemble program

    printfn "%A" assembly
    //let hex = asm.HexDump

    //let dis = asm.Dissasemble hex

    ignore (System.Console.ReadLine())

    0 // return an integer exit code