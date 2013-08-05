namespace Hyperz.SharpLeech.Engine.Core

open System.Reflection
open System.Runtime.CompilerServices
open System.Runtime.InteropServices


module internal AssemblyInfo =
    let Main() = ()
        
    // General Information about an assembly is controlled through the following 
    // set of attributes. Change these attribute values to modify the information
    // associated with an assembly.
    [<assembly: AssemblyTitle("SharpLeech 2 F# Core")>]
    [<assembly: AssemblyDescription("SharpLeech 2 F# Core")>]
    [<assembly: AssemblyConfiguration("")>]
    [<assembly: AssemblyCompany("Hyperz")>]
    [<assembly: AssemblyProduct("SharpLeech 2 F# Core")>]
    [<assembly: AssemblyCopyright("Copyright © Hyperz 2008 - 2010")>]
    [<assembly: AssemblyTrademark("")>]
    [<assembly: AssemblyCulture("")>]

    // Setting ComVisible to false makes the types in this assembly not visible 
    // to COM components.  If you need to access a type in this assembly from 
    // COM, set the ComVisible attribute to true on that type.
    [<assembly: ComVisible(false)>]
    [<assembly: SuppressIldasm()>]

    // The following GUID is for the ID of the typelib if this project is exposed to COM
    //[<assembly: Guid("f3d5b9b5-5c68-4ea5-9589-6399e801a062")>]

    // Version information for an assembly consists of the following four values:
    //
    //      Major Version
    //      Minor Version 
    //      Build Number
    //      Revision
    //
    // You can specify all the values or you can default the Build and Revision Numbers 
    // by using the '*' as shown below:
    // [assembly: AssemblyVersion("1.0.*")]
    [<assembly: AssemblyVersion("2.0.0.0")>]
    [<assembly: AssemblyFileVersion("2.0.0.0")>]
    do Main()