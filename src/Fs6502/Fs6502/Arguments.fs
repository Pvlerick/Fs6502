namespace Fs6502

    type private Arguments =
        | Zero
        | One of byte
        | Two of byte * byte