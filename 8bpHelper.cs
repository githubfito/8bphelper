using System;
using System.IO;
using System.Diagnostics;
using System.Text;
using Microsoft.Win32;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Collections.Generic;


// 0.1e Cambio nombre a 8bpHelper
// 0.1f En Output ahora borro previamente los dsk, map, ihx, asm, lk, lst, noi, rel, sym, bin y HighMemory.lst
// 0.1g Se controla correctamente cuando el archivo.c tiene más de 8 letras+extensión
// 0.1h Pinta resumen
// 0.2a funcionalidad inicial para importación sprites rgas en my_image.asm
// 0.2c primeras pruebas importación rgas funcionando. añado que cuando importas ponga mensaje para que RE-compiles desde winape
// 0.2d añado función -run (ejecuta programa asociado a .dsk), da error si no puede generar el dsk (seguramente por estar en uso)
// 0.2e cuando usas rgas ya acaba y no compila
// 0.2f añado opciones ensamblaje en ayuda
// 0.2g meto función cambio texto en un txt (cambio assembly type in make_all_mygame.asm)
// 0,2h añado opción para que cambie comando save de make_all_mygame según el assembly type
// 0.2i ahora importa bien rgas con sprites en modo 1. (en lugar de que un byte son 2 pixels, en modo 1 un byte son 4 pixels)
// 0.2j Apañao bug al parsear rgas en modo 1
// 0.2k Update to 8bp v43

// version SDCC que funciona bien con 8bp: k14/pdk15 4.1.0 #12072 (MINGW64)
// revisar o buscar línea que contenga .scr y cambiar por load"Pantalla.scr". si no existe buscar primer load y meter antes.
// OK recoger error de SDCC. si hay error que haga pausa y border rojo.
// OK si le metes un argumento con extension .scr que te meta el archivo en el dsk. No podemos añadir automáticamente un load del scr porque no sabemos si será modo 1/2 y si tendrá cambios de paleta
// 1b. meto que pille 8bp.bin desde asm, si no está ahi, lo pilla de la carpeta donde está el código fuente
// 1c. busca 8bp.bin en dos sitios.
//		1) carpeta asm (build by winape despues de meterle la linea save"8bp.bin",23500,19119 a make_all_mygame.asm
//  	2) en la misma carpeta donde está compila y el codigo c del juego
//
// 1d. el archivo C no puede tener más de 8 caracteres para evitar problemas con archivos en disco virtual.


//		* manual *
//		Este programa realiza automáticamente diversos pasos requeridos para compilar un programa con 8bp//
//		* Automáticamente busca un archivo loquesea.c y lo toma como archivo de código
//		* Comprueba si está instalado el compilador SDCC buscando si existe la variable de entorno SDCC

//qué cosas hace 8bpHelper:
//* si ejecutas 8bpHelper sin parametros, te cogerá el primer archivo c que vea. Si solo hay un archiv con extension C pues cogerá ese y como comienzo de memoria definirá lo que diga la variable memoriaStart. Le he puesto 16000 por defecto
//* si ejecutas con 8bphelper mijuego.c, compilará ese código
//* si ejecutas con 8bphelper mijuego.c 8000 compilará ese archivo c con inicio 8000
//* si ejecutas con 8bphelper mijuego.c 8000 con pantalla.scr compilará ese archivo c con inicio 8000 y meterá el scr en el dsk
//* Busca 8bp.bin en dos lugares:
//			1) carpeta asm (build by winape despues de meterle la linea save"8bp.bin",23500,19119 a make_all_mygame.asm
//  		2) en la misma carpeta donde está compila y el codigo c del juego
//* si es la primera vez que se ejecuta 8bphelper, te generará en la misma ruta un loader_base.bas con el cargador del 8bp.bin y del juego.bin. Si el usuario edita este archivo "loader_bas.bas" antes de compilar pues puedes meterle el mode 0/mode1, un load"pantalla.scr", los cambios de paleta (ink), un print"Loading...", etc
//* Comprueba que hay variable de entorno en el sistema llamada SDCC que te la genera el propio compilador durante la instalación, ya sabes 😊
//* Compila el código C con SDCC y convierte el archivo.ihx generado en archivo formato BIN
//* si le pasas un archivo.scr te lo mete en el dsk
//* cada vez que compilas con 8bphelper hace limpia de la carpeta .\output
//* recoge de output\HighMemory.txt la posición más alta para saber si te has pasao, y estás machacando el propio código de la zona 8bp
//* Localiza donde está el main (arcihvo map) para luego hacer correctamente el call del loader_base.bas
//* genera el dsk con el mismo nombre que el fuente. ejemplo: juego33.c —> juego33.dsk

//Al compilarse en vivo y en directo desde winape, no puedo capturar el número de bytes que utiliza cada bloque de los asm (bytes en images_mygame) por ejemplo.
//No sé como hacer un export de los _symbols de la compilación así podría avisar de cuando te pasas.

namespace _8bpjelper
{
    class OchoBPhelper // no le gusta el 8 ahí
    {

//http://www.cpcmania.com/docs/programming/Pintando_pixeles_introduccion_a_la_memoria_de_video.htm
    static string Decode64(string traeCadenaBase64, int traeOrdinal, string traeModoPantalla, int traeAncho, int traeAlto, string traeNombre, bool traeFormatoNumerico) // formato numérico: true=decimal, false=hex
	{
		//string encodedString = "AAAABwcAAAAAAAcAAAAAAAAABwACAAAAAAAHAAAGAAAAAAcAAAAAAAAABwcHAAAAAAAGBgAAAAAAAAAGAAAAAAAGBgYAAAAAAAYABgAADAAADAAGBgYGAAAAAAYAAAAADwAABgAAAAAABgYGBgYAAAAAAAAABgAAAAAAAAAPDwAA";
		//Console.WriteLine("me viene sprite con nombre: "+traeNombre);
		string encodedString; int bytesCount=0;
		encodedString=traeCadenaBase64;
		if (encodedString.Length % 4 > 0) { // debe ser multiplo de 4
			//Console.WriteLine("cadena base64 incorrecta. debe ser multiplo de 4");
			Environment.Exit(0);
		}

		byte[] bytes = Convert.FromBase64String(encodedString);
		string decodedString = Encoding.UTF8.GetString(bytes);
		//Console.WriteLine("Encoded: "+encodedString );			

		for (int n=0;n<bytes.GetUpperBound(0);n++) {
			//Console.WriteLine("Byte "+Convert.ToString(n)+" decoded = "+Convert.ToString(bytes[n]) +", bin= "+Convert.ToString(bytes[n], 2).PadLeft(8, '0')  );				
		}			
		string miModo=traeModoPantalla; int miAncho=traeAncho; int miAlto=traeAlto; string spriteNombre=traeNombre;
		int miAnchoTemp, miAltoTemp; miAltoTemp=0;
		string pixel0, pixel1, pixel2, pixel3; string lineaDefs="";
		string byteFinalString; int byteFinalInt; string byteFinalHex; string Sumatorio="";
		if (miModo.Equals("0" ) ) {
			//Console.WriteLine("upperbound de nbytes: "+bytes.GetUpperBound(0));
			if (bytesCount>traeAncho) {
				Console.WriteLine("Malamante");
			}
			//Console.WriteLine("En definición de nombre de sprites: dw "+spriteNombre); lineaDefs="";
            //Console.WriteLine(traeNombre); 
            Sumatorio = Sumatorio + traeNombre + " ; id "+(traeOrdinal+16)+"\n";
			//Console.WriteLine("db "+miAncho/2+"; ancho sprite"); 
			if (!traeFormatoNumerico)		 //decimal
			{
				Sumatorio = Sumatorio + "db " + miAncho / 2 + "; ancho sprite\n";
				//Console.WriteLine("db "+miAlto+"; alto sprite"); 
				Sumatorio = Sumatorio + "db " + miAlto + "; alto sprite\n";
			}
			else
			{			                  // hex
                Sumatorio = Sumatorio + "db #" + (miAncho / 2).ToString("X2") + "; ancho sprite\n";
                //Console.WriteLine("db "+miAlto+"; alto sprite"); 
                Sumatorio = Sumatorio + "db #" + miAlto.ToString("X2") + "; alto sprite\n";
            }

			miAnchoTemp=0;
			for (int n=0;n<bytes.GetUpperBound(0);n+=2) {
				byteFinalString="";
				//Console.WriteLine("byte pair pos "+Convert.ToString(n)+" decoded = "+Convert.ToString(bytes[n]) +", bin= "+Convert.ToString(bytes[n], 2) + " AND pos " + Convert.ToString(n+1)+" "+Convert.ToString(bytes[n+1]) +", bin= "+Convert.ToString(bytes[n+1], 2).PadLeft(8,'0'));					
				pixel0=Convert.ToString(bytes[n], 2).PadLeft(8,'0');
				pixel1=Convert.ToString(bytes[n+1], 2).PadLeft(8,'0');
				byteFinalString=byteFinalString+pixel0.Substring(7,1)+pixel1.Substring(7,1);	//pixel 0 (bit 0)+pixel 1 (bit 0)
				//Sumatorio=Sumatorio+pixel0.Substring(7,1)+pixel1.Substring(7,1);
				byteFinalString=byteFinalString+pixel0.Substring(5,1)+pixel1.Substring(5,1);	//pixel 0 (bit 2)+pixel 1 (bit 2)
				//Sumatorio=Sumatorio+pixel0.Substring(5,1)+pixel1.Substring(5,1);
				byteFinalString=byteFinalString+pixel0.Substring(6,1)+pixel1.Substring(6,1);	//pixel 0 (bit 1)+pixel 1 (bit 1)
				//Sumatorio=Sumatorio+pixel0.Substring(6,1)+pixel1.Substring(6,1);
				byteFinalString=byteFinalString+pixel0.Substring(4,1)+pixel1.Substring(4,1);	//pixel 0 (bit 3)+pixel 1 (bit 3)
				//Sumatorio=Sumatorio+pixel0.Substring(4,1)+pixel1.Substring(4,1);
				byteFinalInt=Convert.ToInt32(byteFinalString,2);
				byteFinalHex=byteFinalInt.ToString("X2");
                //Console.WriteLine("amstrad mode 0 final byte pair = "+byteFinalString.ToString()+" = "+byteFinalInt+", hex = "+byteFinalHex);					
                //lineaDefs = lineaDefs + byteFinalHex;
				if (!traeFormatoNumerico) 
					lineaDefs = lineaDefs + byteFinalInt; // decimal
				else
                    lineaDefs = lineaDefs + "#"+byteFinalHex; // hex
                miAnchoTemp +=2;
				//Console.WriteLine("miAnchoTemp es "+miAnchoTemp);
				if (miAnchoTemp + 1 < miAncho)
				{
					lineaDefs = lineaDefs + ", ";
					bytesCount += 2;
				}
				else
				{
					miAnchoTemp = 0; miAltoTemp++;
					//Console.WriteLine("db " + lineaDefs);
					Sumatorio = Sumatorio + "db " + lineaDefs + "\n";
					lineaDefs = "";
					if (miAltoTemp == traeAlto) {
						Sumatorio = Sumatorio + "\n";
						break;
					}
				}
			}
            return Sumatorio;
		}				
		
		if (miModo.Equals("1" ) ) {
			//Console.WriteLine("upperbound de nbytes: "+bytes.GetUpperBound(0));
			if (bytesCount>traeAncho) {
				Console.WriteLine("Malamante");
			}
			//Console.WriteLine("En definición de nombre de sprites: dw "+spriteNombre); lineaDefs="";
            //Console.WriteLine(traeNombre); 
            Sumatorio = Sumatorio + traeNombre + " ; id "+(traeOrdinal+16)+"\n";
			string recordatorio = " ---> Remember. mode 1 is 4 pixels per byte. mode 0 is 2 pixels per byte\n";
			if (!traeFormatoNumerico)		 //decimal
			{
				Sumatorio = Sumatorio + "db " + miAncho / 4 + "; sprite width"+recordatorio;
				//Console.WriteLine("db "+miAlto+"; alto sprite"); 
				Sumatorio = Sumatorio + "db " + miAlto + "; sprite height\n";
			}
			else
			{			                  // hex
                Sumatorio = Sumatorio + "db #" + (miAncho / 4).ToString("X2") + "; sprite width"+recordatorio;
                //Console.WriteLine("db "+miAlto+"; alto sprite"); 
                Sumatorio = Sumatorio + "db #" + miAlto.ToString("X2") + "; sprite height\n";
            }

			miAnchoTemp=0;
			for (int n=0;n<bytes.GetUpperBound(0);n+=4) {
				byteFinalString="";
				//Console.WriteLine("byte pair pos "+Convert.ToString(n)+" decoded = "+Convert.ToString(bytes[n]) +", bin= "+Convert.ToString(bytes[n], 2) + " AND pos " + Convert.ToString(n+1)+" "+Convert.ToString(bytes[n+1]) +", bin= "+Convert.ToString(bytes[n+1], 2).PadLeft(8,'0'));
				pixel0=Convert.ToString(bytes[n]  , 2).PadLeft(8,'0');
				pixel1=Convert.ToString(bytes[n+1], 2).PadLeft(8,'0');
				pixel2=Convert.ToString(bytes[n+2], 2).PadLeft(8,'0');
				pixel3=Convert.ToString(bytes[n+3], 2).PadLeft(8,'0');

				byteFinalString=byteFinalString+pixel0.Substring(7,1)+pixel1.Substring(7,1);
				byteFinalString=byteFinalString+pixel2.Substring(7,1)+pixel3.Substring(7,1);
				byteFinalString=byteFinalString+pixel0.Substring(6,1)+pixel1.Substring(6,1);
				byteFinalString=byteFinalString+pixel2.Substring(6,1)+pixel3.Substring(6,1);				

				byteFinalInt=Convert.ToInt32(byteFinalString,2);
				byteFinalHex=byteFinalInt.ToString("X2");
                //Console.WriteLine("amstrad mode 0 final byte pair = "+byteFinalString.ToString()+" = "+byteFinalInt+", hex = "+byteFinalHex);					
                //lineaDefs = lineaDefs + byteFinalHex;
				if (!traeFormatoNumerico) 
					lineaDefs = lineaDefs + byteFinalInt; // decimal
				else
                    lineaDefs = lineaDefs + "#"+byteFinalHex; // hex
                miAnchoTemp +=4;
				//Console.WriteLine("miAnchoTemp es "+miAnchoTemp);
				if (miAnchoTemp + 1 < miAncho)
				{
					lineaDefs = lineaDefs + ", ";
					bytesCount += 4;
				}
				else
				{
					miAnchoTemp = 0; miAltoTemp++;
					//Console.WriteLine("db " + lineaDefs);
					Sumatorio = Sumatorio + "db " + lineaDefs + "\n";
					lineaDefs = "";
					if (miAltoTemp == traeAlto) {
						Sumatorio = Sumatorio + "\n";
						break;
					}
				}
			}
            return Sumatorio;
		}					
		return "";
	}	
	static public bool IsNumeric(string text)
	{
        double test;
        return double.TryParse(text, out test);
	}
	static public void ModificaAsm(string traeNombreAsm, string parrafoStart, string parrafoEnd, string textoInsertar)
	{
		System.IO.StreamWriter destFile;
		string nombreDestino=Path.GetDirectoryName(@traeNombreAsm)+"\\destino.asm";
		//Console.WriteLine("nombre destino es: "+nombreDestino);
		destFile = new System.IO.StreamWriter(nombreDestino);
		using (StreamReader ReaderObject = new StreamReader(traeNombreAsm))
		{
			string Line; bool parrafoStarted=false; bool parrafoEnded=false;
			// ReaderObject reads a single line, stores it in Line string variable and then displays it on console
			while((Line = ReaderObject.ReadLine()) != null)
			{
				//'Console.WriteLine(Line);
				if (Line.Contains(parrafoStart) ) {
					//Console.WriteLine("encontrado start parrafo con ["+parrafoStart+"]. Añadiendo texto...");
					parrafoStarted=true;
					destFile.WriteLine(parrafoStart);
					destFile.WriteLine(";========== sprites added from 8bphelper "+ System.DateTime.Now.ToString("dd.MM.yy hh.ss") +" ================");
					destFile.WriteLine(textoInsertar);
				}
				if (Line.Contains(parrafoEnd) ) {
					//Console.WriteLine("encontrado end parrafo con ["+parrafoEnd+"]");				
					parrafoEnded=true;
				}				  
				if (!parrafoStarted || parrafoEnded)
					destFile.WriteLine(Line);				  
			}
		}
		destFile.Flush(); destFile.Close();
        File.Delete(traeNombreAsm); File.Move(nombreDestino, traeNombreAsm);
    }

	static public void cambioTipoEnsamblaje(string numEnsamblajeStr, string traeStart, string traeLargo)
	{
		string archivo = "..\\asm\\make_all_mygame.asm";
		string textoBuscar = "LET ASSEMBLING_OPTION"; // cambiar por let ASSEMBLING_OPTION = X (X=0,1,2,3,4) PONER EN MAYS
        string textoReemplazar = "let ASSEMBLING_OPTION = " + numEnsamblajeStr;

		try
		{
			string[] lineas = File.ReadAllLines(archivo);

			for (int i = 0; i < lineas.Length; i++)
			{
				if (lineas[i].ToUpper().Contains(textoBuscar))
				{
					lineas[i] =textoReemplazar;
                }
                if (lineas[i].ToUpper().Contains("SAVE\""))
                {
                    lineas[i] = ";borraestoperoya";
                }
            }
			List<string> listaValores = new List<string>(lineas);
			listaValores.Remove(";borraestoperoya"); // Elimina el elemento
			lineas = listaValores.ToArray(); // Convierte de nuevo 			

            File.WriteAllLines(archivo, lineas);
            File.AppendAllText(archivo, "Save\"8bp.bin\", " + traeStart+", "+traeLargo+" ;added by 8BPHelper"+ Environment.NewLine);


            Console.WriteLine("File "+archivo+" update successfully.");
		}
		catch (Exception ex)
		{
			Console.WriteLine("Error: "+ex.Message+" reading {archivo}");
		}
	}

	// Ensure this is the only Main method in your project.
	static void Main(string[] args)
	{
		
		//ModificaAsm(@"C:\\Users\\FITO\\Desktop\\AACPC\\8BP_V42\\ASM\\images_mygame.asm", @"_BEGIN_IMAGES", @"_END_IMAGES","@Esto\nes\nuna\npruebecilla\n");
		uint memoriaStart = 16000; // se puede cambiar desde modo comando como parámetro

		string MiVersion = "0.2k";

		string EnsamblajeTipo = "-1";
		uint Empieza8bpInt = 23600; // ojo si cambios este cambia tambien el otro de abajo
		string Empieza8bpString; // = "23500"; // ojo si cambios este cambia tambien el otro de arriba			
		Empieza8bpString = Empieza8bpInt.ToString(); // ojo si cambios este cambia tambien el otro de arriba
		string Longitud8bpString = "19020"; // ojo si cambios este cambia tambien el otro de arriba

		
		System.Diagnostics.Process process;
		System.IO.StreamWriter destFile;
		string Fuente = ""; // nombre archivo codigo fuente que se va a compilar
		string rgasFile = ""; // nombre archivo rgas con los sprites a añadir al my_images....asm
		string FuenteSinExtension = ""; // nombre archivo codigo fuente sin extension
		string Pantalla="";
		int andepara8bpbin=0;
		string andepara8bpbinString=""; bool RecordatorioCompilar = false;
		string suma = ""; string Argumentos = "";

		bool FormatoNumerico = false; // pinta los db bytes en images.asm en decimal. si es true pintara en hex
		bool Ejecutalo = false; // si pones argumento -RUN y ha creado OK el dsk, entonces lo ejecuta (si está asociado a un emulador)
		bool compilarBandera = true; // si es true, no compila con abasm y utilizará el 8bp.bin que ya exista

        string path = Directory.GetCurrentDirectory();
		DirectoryInfo pathPadre = Directory.GetParent(path);
		//Console.WriteLine("path es: [" + path + "]");
		//Console.WriteLine("pathPadre es: [" + pathPadre + "]");

		Console.WriteLine("8bpHelper " + MiVersion+"\r\r");		

		for (int inv=0;inv<args.Length;inv++)
		{
			if (args[inv].ToUpper().Contains("-H") || args[inv].ToUpper().Contains("/H") || args[inv].ToUpper().Contains("/?") )
			{
				Console.WriteLine("fitosoft 2022\r");
				Console.WriteLine("8bpHelper for 8bp (8 bits de poder)\r\r");
				Console.WriteLine("    format: 8bpHelper.exe name.c    5000   screen.scr -rgashex -rgas=\"rgas datafile path\" [-a0] [-a1] [-a2] [-a3] [-nocompile]");
				Console.WriteLine("    *  name.c -------> program to compile\r");
				Console.WriteLine("    *  5000 ---------> User Start address\r");
				Console.WriteLine("    *  screen.scr ---> name of screen scr for adding to dsk. Auto-configured loader to show cover\r\r");
				Console.WriteLine("    -rgashex --------> import rgas data file as hex byte data\r\r");
				Console.WriteLine("    -rgas= ----------> import rgas data file to asm\\images_mygame.asm sprites info file! \r\r");
				Console.WriteLine("    -run ------------> run associated DSK program (beta)\r\r");
				Console.WriteLine("    -aX = 8bp Assembly option. Depending on the option, you will gain memory or lose commands\r\r");
				Console.WriteLine("                  -a0 Option 0. All comands. 23600/19020 (default)\r\r");
				Console.WriteLine("                  -a1 Option 1. Unavailable: scroll,3d.  25000/17620\r\r");
				Console.WriteLine("                  -a2 Option 2. Unavailable: layout, colay, 3d. 24800/17820\r\r");
				Console.WriteLine("                  -a3 Option 3. Unavailable: layout, colay.     24000/18620\r\r");
                Console.WriteLine("                  -a4 Option 4. Unavailable: layout, colay, 3d, scroll.    25300/17320\r\r");
                    Console.WriteLine("                  -nocompile. Skip running BASM to compile. The existing one in asm folder or source folder will be used \r");
				Console.WriteLine("    The loader 'loader_base.bas' will be used to load 8bp.bin and then load the user code. It can also be used to load extra files. if loader does not exist, one will be created by default");
				Console.WriteLine("    Sample: 8bphelper.exe 5000 -rgashex -rgas=C:\\Users\\FITO\\Desktop\\fitman\\RGAS\\fitman.RGAS -run");

				Environment.Exit(0);
			}
			if (!IsNumeric(args[inv])) {
                    if (args[inv].ToUpper().Contains("-NOCOMPILE"))
                    {
                        Console.WriteLine("Skippping ABASM Compile");
						compilarBandera = false;
                    }
                    if (args[inv].ToUpper().Contains("-A0")) {
					Console.WriteLine("Using Assembly type 0 with start 23600, size 19020 ");
					cambioTipoEnsamblaje("0","23600","19020");
					Console.WriteLine("End changing Assembly type!!");
					EnsamblajeTipo = "0";
					Empieza8bpInt = 23500; // ojo si cambios este cambia tambien el otro de abajo
					Empieza8bpString = Empieza8bpInt.ToString(); // ojo si cambios este cambia tambien el otro de arriba
					Longitud8bpString = "19020"; // ojo si cambios este cambia tambien el otro de arriba
				}
				if (args[inv].ToUpper().Contains("-A1"))
				{
					Console.WriteLine("Using Assembly type 1 with start 25000, size 17620");
					cambioTipoEnsamblaje("1","25000","17620");
					Console.WriteLine("End changing Assembly type!!");
					EnsamblajeTipo = "1";
					Empieza8bpInt = 25000; // ojo si cambios este cambia tambien el otro de abajo
					Empieza8bpString = Empieza8bpInt.ToString(); // ojo si cambios este cambia tambien el otro de arriba
					Longitud8bpString = "17620"; // ojo si cambios este cambia tambien el otro de arriba    
				}
				if (args[inv].ToUpper().Contains("-A2"))
				{
					Console.WriteLine("Using Assembly type 2 with start 24800, size 17820");
					cambioTipoEnsamblaje("2","24800","17820");
					Console.WriteLine("End changing Assembly type!!");
					EnsamblajeTipo = "2";
					Empieza8bpInt = 24800; // ojo si cambios este cambia tambien el otro de abajo
					Empieza8bpString = Empieza8bpInt.ToString(); // ojo si cambios este cambia tambien el otro de arriba
					Longitud8bpString = "17820"; // ojo si cambios este cambia tambien el otro de arriba    
				}
				if (args[inv].ToUpper().Contains("-A3"))
				{
					Console.WriteLine("Using Assembly type 3 with start 24000, size 18620");
					cambioTipoEnsamblaje("3","24000","18620");
					Console.WriteLine("End changing Assembly type!!");
					EnsamblajeTipo = "3";
					Empieza8bpInt = 24000; // ojo si cambios este cambia tambien el otro de abajo
					Empieza8bpString = Empieza8bpInt.ToString(); // ojo si cambios este cambia tambien el otro de arriba
					Longitud8bpString = "18620"; // ojo si cambios este cambia tambien el otro de arriba    
				}
                if (args[inv].ToUpper().Contains("-A4"))
                {
                    Console.WriteLine("Using Assembly type 4 with start 25300, size 17320");
                    cambioTipoEnsamblaje("4", "25300", "17320");
                    Console.WriteLine("End changing Assembly type!!");
                    EnsamblajeTipo = "4";
                    Empieza8bpInt = 25300; // ojo si cambios este cambia tambien el otro de abajo
                    Empieza8bpString = Empieza8bpInt.ToString(); // ojo si cambios este cambia tambien el otro de arriba
                    Longitud8bpString = "17320"; // ojo si cambios este cambia tambien el otro de arriba    
                }
                    if (args[inv].ToUpper().Contains(".SCR")) {						
					Pantalla = args[inv];
					Console.WriteLine("Inserting the load of screen " + Pantalla+" in loader_base.bas");
					if (!File.Exists(Pantalla)) {
						Console.WriteLine("ERROR: Screen " + Pantalla + " specified not Found\r");
						Environment.Exit(1);
					}
				}
				if (args[inv].ToUpper().Contains("-RGASHEX")) {
					Console.WriteLine("OK. db bytes in hex format!");
					FormatoNumerico = true;
				}
				if (args[inv].ToUpper().Contains("-RUN")) { 
					Ejecutalo = true;
				}
				if (args[inv].ToUpper().Contains("-RGAS="))
				{
					//Console.WriteLine("argument: "+args[inv]+"\n");
					string ModoPantalla="";
					rgasFile = args[inv].Substring(6);
					if (!File.Exists(rgasFile)) {
						Console.WriteLine("File "+rgasFile+" Not Found .................. NOK");
						Console.ReadLine();
						Environment.Exit(1);
					}
					else {
						Console.WriteLine("Importing "+rgasFile+" .............................FOUND OK");


						string[] fileContents2 = File.ReadAllLines(rgasFile);
						string stringmatch2 = Array.Find (fileContents2, delegate (string name) { return name.Contains ("\"Mode\":"); } );
						if (!String.IsNullOrEmpty(stringmatch2) ) {									
								ModoPantalla=stringmatch2.Substring(10,1);
								Console.WriteLine("Screen Mode=" + ModoPantalla + " detected");
						}

						int spritesCount=0;
						using (StreamReader ReaderObject = new StreamReader(rgasFile))								// parse file .rgas to obtain sprite name, width, height, and bytes
						{
							string Line; bool imageListStart=false; bool imageListEnd=false; string rgasWidth, rgasHeight, rgasNombre, rgas64; rgasWidth=""; rgasHeight=""; rgasNombre=""; rgas64=""; 
							string nombresCabecera="";	string bloqueSprites = "";
							// ReaderObject reads a single line, stores it in Line string variable and then displays it on console
							while((Line = ReaderObject.ReadLine()) != null)
							{
								//'Console.WriteLine(Line);

								if (Line.Contains("_ImageList\": {")) {
									imageListStart=true;
									//Console.WriteLine("encontrado ImageListSTart");
								}
								if (imageListStart && !imageListEnd) { // encuentro fin parrafo de definición de sprites
									if (Line.Equals("  },")) {
										imageListEnd=true;
										//Console.WriteLine("encontrado ImageListEnd");
									}	
								}
								if (imageListStart && !imageListEnd) { // encuentro variables de sprite dentro del bloque ImageList
									if (Line.Contains("\"Width\":")) {
										rgasWidth=Line.Substring(17).Replace(",","");
										//Console.WriteLine("encontrado width: "+Line+", corto="+rgasWidth);										  
									}
									if (Line.Contains("\"Height\":")) {
										rgasHeight=Line.Substring(18).Replace(",","");
										//Console.WriteLine("encontrado Height: "+Line+", corto="+rgasHeight);										  
									}			
									if (Line.Contains("\"_name\":")) { 									// con esta variable acabamos de leer nuestros datos del parrafo del sprite actual y decodificamos
										rgasNombre=Line.Substring(18).Replace("\",","");
										//Console.WriteLine("encontrado nombre: "+Line+", corto="+rgasNombre);
										nombresCabecera=nombresCabecera+"DW "+rgasNombre+" ; "+(16+spritesCount)+"........... height "+rgasHeight+", Width "+rgasWidth+"\n";

										//static string Decode64(string traeCadenaBase64, string traeModoPantalla, int traeAncho, int traeAlto, string traeNombre)
										bloqueSprites = bloqueSprites + Decode64(rgas64, spritesCount, ModoPantalla, Int32.Parse(rgasWidth), Int32.Parse(rgasHeight), rgasNombre, FormatoNumerico);
										//Console.WriteLine(Decode64(rgas64, ModoPantalla, Int32.Parse(rgasWidth), Int32.Parse(rgasHeight), rgasNombre) );
										spritesCount++;
									}	
									if (Line.Contains("          \"$value\":")) {
										rgas64=Line.Substring(21).Replace("\"","");
										//Console.WriteLine("encontrado valor base16: "+Line+", corto="+rgas64);										  
									}										  
																	
								} 
							}
							Console.WriteLine(spritesCount + " sprites encontrados.");
							//Console.WriteLine("La cabecera de nombres será:"); 
							Console.WriteLine(nombresCabecera);
							string NombreAsmSprites = @"..\\asm\\images_mygame.asm";
							ModificaAsm(@NombreAsmSprites, "IMAGE_LIST", "_BEGIN_ALPHABET", nombresCabecera);
							ModificaAsm(@NombreAsmSprites, "_BEGIN_IMAGES", "_END_IMAGES", bloqueSprites);
							Console.WriteLine(spritesCount + " sprites updated in " + NombreAsmSprites);
							Console.WriteLine("End...");
						}
						
						RecordatorioCompilar = true;
						//Environment.Exit(1);
					}
				}					

				if (args[inv].ToUpper().Contains(".C"))
				{
					Console.WriteLine("argument: "+args[inv]+"\n");
					Fuente = args[inv];
					if (!File.Exists(Fuente)) {
						Console.WriteLine("Source code "+Fuente+" Not Found .................. NOK");
						Console.ReadLine();
						Environment.Exit(1);
					}
				}

				

			}
			else {
				Console.WriteLine("Numeric ARgument detected: "+args[inv]+"\r");
				memoriaStart=UInt16.Parse(args[inv]);
				
				//Environment.Exit(1);
			}
			
		}

		if (EnsamblajeTipo=="-1")
		{
			Console.WriteLine("Using Assembly type 0 with start 23500, size 19119 ");
			cambioTipoEnsamblaje("0", "23500", "19119");
			Console.WriteLine("End changing Assembly type!!");
			EnsamblajeTipo = "0";
			Empieza8bpInt = 23500; // ojo si cambios este cambia tambien el otro de abajo
			Empieza8bpString = Empieza8bpInt.ToString(); // ojo si cambios este cambia tambien el otro de arriba
			Longitud8bpString = "19119"; // ojo si cambios este cambia tambien el otro de arriba
		}

		// COMPROBAR SI EXISTE abasm.py
		Console.WriteLine("Searching ABASM.py in .\abasm..................\r");
		if (File.Exists(path + @"\Abasm\src\abasm.py"))
		{
			Console.WriteLine(path + @"\abasm\src\abasm.py found............OK");
		}
		else
		{
			Console.WriteLine(path + @"\abasm\src\abasm.py NOT found");
			Environment.Exit(1);
		}
		// COMROBAR EXISTEN CARPETA y archivo ..\ASM\make_all_mygame.asm
		Console.WriteLine("Searching 8BP ASM\\make_all_mygame.asm..................\r");
		if (File.Exists(pathPadre + @"\ASM\make_all_mygame.asm"))
		{
			Console.WriteLine(pathPadre + @"\ASM\make_all_mygame.asm found.............OK");
		}
		else
		{
			Console.WriteLine(pathPadre + @"\ASM\make_all_mygame.asm not found\n");
			Environment.Exit(1);
		}
		// COMPROBAR EXISTE CARPETA CON PYTHON EN CARPETA .\PYTHON
		Console.WriteLine("Searching PYTHON portable dir..................\r");
		if (File.Exists(path + @"\python\python.exe"))
		{
			Console.WriteLine(path + @"\python\python.exe found............OK");
		}
		else
		{
			Console.WriteLine(path + @"\PYTHON\python.exe not found\n");
			Environment.Exit(1);
		}

		if (compilarBandera)
		{
			suma = path + @"\python\python.exe";
			Argumentos = path + @"\abasm\src\abasm.py -t2 " + pathPadre + @"\ASM\make_all_mygame.asm";

			Console.WriteLine("Running ABASM Command: ");
			Console.WriteLine(suma + " " + Argumentos + "\r");
			Process p3 = new Process(); // Redirect the output stream of the child process. 
			p3.StartInfo.UseShellExecute = false;
			//p.StartInfo.RedirectStandardOutput = true; 
			//procStartInfo.RedirectStandardError = true;
			//p.StartInfo.Redirect = true; 
			p3.StartInfo.FileName = suma;
			p3.StartInfo.Arguments = Argumentos;
			p3.Start(); // Do not wait for the child process to exit before // reading to the end of its redirected stream. // 
						//string output = p.StandardOutput.ReadToEnd(); 
						//string outputError= p.StandardError.ReadToEnd(); 
			p3.WaitForExit();
			//Console.ReadLine();					 //espera tecla
			if (p3.ExitCode > 0)
			{

				//Console.WriteLine(output);				
				//Console.WriteLine(outputError);		
				Console.WriteLine("ERROR Compiling: Press enter");
				Console.ReadLine(); //espera tecla
				Environment.Exit(1);
			}
			// fin compilación con abasm
		}

		if (Fuente=="")  {
			DirectoryInfo d = new DirectoryInfo(@"."); //Assuming Test is your Folder
			FileInfo[] Files = d.GetFiles("*.c"); //Getting Text files				
			foreach(FileInfo file in Files )
			{
				if (!file.Name.ToUpper().Contains("8BPHELPER.C")) { // porsiaca durante las pruebas de este programa						
					Console.WriteLine("No source file specified. Using "+file.Name);
					if ( Path.GetFileNameWithoutExtension(file.Name).Length>8 ) {
						Console.WriteLine("oops! Filename %s too long. Max 8 characters with .C extension to avoid issues with cpc disk emulation",file.Name);
						Environment.Exit(1);
					}
					Console.WriteLine("Using "+memoriaStart.ToString()+" as start load (Add decimal address as numeric argument to change value) \r");
					Fuente=file.Name;
					break;
				}
			}				
			if (Fuente=="") {
				Console.WriteLine("No sources files .c found in this directory!!");
				Console.ReadLine();
				Environment.Exit(1);
			}
		}
		FuenteSinExtension = Path.GetFileNameWithoutExtension (Fuente);			
		if ( Path.GetFileNameWithoutExtension(Fuente).Length>8 ) {
			Console.WriteLine("filename "+Fuente+" too long. Max 8 characters and .C to avoid issues with cpc disk emulation!");
			Environment.Exit(1);
		}			

		//string Atras= Path.GetDirectoryName(path);
//         string[] fileContents = File.ReadAllLines(Atras+"\\asm\\make_all_mygame.asm");
//         string stringmatch = Array.Find(fileContents, delegate (string name) { return name.ToUpper().Contains("SAVE\"8BP.BIN\""); });
//         if (String.IsNullOrEmpty(stringmatch))
//         {
//             Console.WriteLine("Check "+Atras+"\\asm\\make_all_mygame.asm. You must add the hack SAVE\"8bp.bin\",b," + Empieza8bpString + "," + Longitud8bpString + ",&6b78 at the end of file so that 8bphelper will be able to access 8bp.bin!");
//             Environment.Exit(1);
//         }
//         Console.WriteLine("Checking save hack in make_all_mygame......OK");

		try {
			Console.WriteLine("Cleaning output dir...(dsk, map, ihx, asm) ...\r");
			
			// 20240630 aHORA BORRO estos tipos de archivos pero no solo con nombre archivo C especificado, si no de todos
			//System.IO.File.Delete(".\\output\\*.dsk");

			DirectoryInfo folder = new DirectoryInfo(@".\\output\\");
			if (folder.Exists) // else: Invalid folder!
			{
				FileInfo[] files = folder.GetFiles("*.dsk"); foreach (FileInfo file in files) File.Delete(file.FullName);
				files = folder.GetFiles("*.map"); foreach (FileInfo file in files) File.Delete(file.FullName); 
				files = folder.GetFiles("*.ihx"); foreach (FileInfo file in files)  File.Delete(file.FullName); 
				files = folder.GetFiles("*.asm"); foreach (FileInfo file in files)  File.Delete(file.FullName); 
				files = folder.GetFiles("*.lk"); foreach (FileInfo file in files) File.Delete(file.FullName); 
				files = folder.GetFiles("*.lst"); foreach (FileInfo file in files) File.Delete(file.FullName); 
				files = folder.GetFiles("*.noi"); foreach (FileInfo file in files) File.Delete(file.FullName); 
				files = folder.GetFiles("*.rel"); foreach (FileInfo file in files) File.Delete(file.FullName); 
				files = folder.GetFiles("*.sym"); foreach (FileInfo file in files) File.Delete(file.FullName); 
				files = folder.GetFiles("*.bin"); foreach (FileInfo file in files) File.Delete(file.FullName); 
				files = folder.GetFiles("*.bas"); foreach (FileInfo file in files) File.Delete(file.FullName); 
				files = folder.GetFiles("HIGHMEMORY.TXT"); foreach (FileInfo file in files) File.Delete(file.FullName);
			}
			else
			{
				Console.WriteLine("output folder not found!!...");	
								Console.ReadLine();
								Environment.Exit(1);	
			}
			//
			//System.IO.File.Delete(".\\output\\"+FuenteSinExtension+".dsk");
			//System.IO.File.Delete(".\\output\\"+FuenteSinExtension+".map");
			//System.IO.File.Delete(".\\output\\"+FuenteSinExtension+".ihx");
			//System.IO.File.Delete(".\\output\\"+FuenteSinExtension+".asm");
			//System.IO.File.Delete(".\\output\\"+FuenteSinExtension+".LK");
			//System.IO.File.Delete(".\\output\\"+FuenteSinExtension+".lst");
			//System.IO.File.Delete(".\\output\\"+FuenteSinExtension+".noi");
			//System.IO.File.Delete(".\\output\\"+FuenteSinExtension+".rel");
			//System.IO.File.Delete(".\\output\\"+FuenteSinExtension+".sym");
			//System.IO.File.Delete(".\\output\\"+FuenteSinExtension+".bin");
		} catch (Exception ex) 
		{
			Console.WriteLine("ERROR cleaning output dir: "+ex.Message);				
			//Console.ReadLine();
			//Environment.Exit(1);				
		};
			
		if (!File.Exists("HEX2BIN.EXE") || !File.Exists("MANAGEDSK.EXE")) {
			Console.WriteLine("ERROR: hex2bin.exe or managedsk.exe not found");
			Environment.Exit(1);
		}			
		

		Console.WriteLine("Trying to compile "+Fuente+"\r");			
		string rutaSDCC ="";
		Argumentos="-mz80 --verbose --code-loc "+Convert.ToString(memoriaStart, 10)+" --data-loc 0 --no-std-crt0 ";
		Argumentos=Argumentos+"--fomit-frame-pointer --opt-code-size -I8BP_wrapper -Imini_BASIC -o output\\ "+Fuente;
		Console.WriteLine("Searching SDCC path ........................!");
		string traePath = Environment.GetEnvironmentVariable("path");
		if (traePath.Length==0) {
			Console.WriteLine("No environment path!");
			Console.ReadLine();
			Environment.Exit(1);
		}
		string[] subs = traePath.Split(';');
		suma="";
		foreach ( string cual in subs) {				
			if (cual.ToUpper().Contains("SDCC")) {
				Console.WriteLine("Found SDCC path entry: "+cual.ToString()+"\r");
				rutaSDCC =cual.ToString();
				//suma=rutaSDCC+"\\sdcc.exe -mz80 --verbose --code-loc 20000 --data-loc 0 --no-std-crt0 --fomit-frame-pointer --opt-code-size -I8BP_wrapper -Imini_BASIC -o output/ ";
				suma=suma+Fuente;
				suma=rutaSDCC+"\\sdcc.exe";
				suma=suma.Replace("\\","\\\\");

				//Console.WriteLine("ejecuto: \r"+suma+"\r");
				break;
			}				
		}
		if (rutaSDCC=="rutaSDCC") {
			Console.WriteLine("SDCC entry in system path NOT FOUND!! InStall SDCC and add SDCC-Path to System Path variable");
			Console.ReadLine();
			Environment.Exit(1);				
		}
		
		//Console.ReadLine();
		
		
		
		//			****************************************
		Console.WriteLine("SDCC Compile...... "+suma+" "+Argumentos+"\r");
		Process p = new Process(); // Redirect the output stream of the child process. 
		p.StartInfo.UseShellExecute = false; 
		//p.StartInfo.RedirectStandardOutput = true; 
		//procStartInfo.RedirectStandardError = true;
		//p.StartInfo.Redirect = true; 
		p.StartInfo.FileName = suma;
		p.StartInfo.Arguments = Argumentos;
		p.Start(); // Do not wait for the child process to exit before // reading to the end of its redirected stream. // 
		//string output = p.StandardOutput.ReadToEnd(); 
		//string outputError= p.StandardError.ReadToEnd(); 
		p.WaitForExit();
		if (p.ExitCode>0) {
				
			//Console.WriteLine(output);				
			//Console.WriteLine(outputError);		
			Console.WriteLine("ERROR Compiling: Press enter");
			Console.ReadLine();
			Environment.Exit(1);								
		}	


		//			***********			
				
		
		
		/*
		Console.WriteLine("SDCC Compile...... "+suma+" "+Argumentos+"\r");
		process = System.Diagnostics.Process.Start(suma,Argumentos);
		while (!process.HasExited)
			{
			//update UI
			}
		Console.WriteLine("  Process exit code          : {0:D}\r", process.ExitCode);
		if (process.ExitCode>0) {
				
			Console.WriteLine(output);				
			Console.WriteLine("ERROR Compiling: Press enter");
			Console.ReadLine();
			Environment.Exit(0);								
		}			   
		*/

		//Console.ReadLine();			
		
		
		Console.WriteLine("The current directory is {0}", path);		
		
		// COMPROBAR SI EXISTE 8BP.BIN
		Console.WriteLine("Searching 8BP.BIN (asm folder or current folder)..................\r");
		if (File.Exists("..\\ASM\\8BP.BiN")) {
			Console.WriteLine("..\\ASM\\8BP.BIN found (preferred).............OK");
			andepara8bpbin=1;
			andepara8bpbinString="ASM folder";
		}
		else
		{
			if (File.Exists("8bp.bin") ) {
				Console.WriteLine("8BP.BIN found in current source dir........OK");
				andepara8bpbin=2;
				andepara8bpbinString="root folder (C source folder)";
			}
			else { 
				Console.WriteLine("8BP.BIN is neither on ASM level nor on current directory\n"+ 
				"Save with next command: SAVE \"8bp.bin\",b,"+Empieza8bpString+","+Longitud8bpString+",&6b78 and paste to source directory level\n"+
				"or add this line to [make_all_mygame.asm] for compiling to file in asm folder from winape.",Empieza8bpString);
				Environment.Exit(1);
			}
		}					
		
		// COMPROBAR SI SE HA GENERADO EL ARCHIVO MAP
		if (File.Exists("output\\"+FuenteSinExtension+".map")) {
			Console.WriteLine(FuenteSinExtension+".map found................OK");
		}
		else {
			Console.WriteLine("ERROR Compiling: FuenteSinExtension"+FuenteSinExtension+".map not found!. Press enter");
			Console.ReadLine();
			Environment.Exit(1);				
		}            
		
		Console.WriteLine("Translating .ihx to "+FuenteSinExtension+".BIN");			
		try {
			System.IO.File.Delete(".\\output\\HighMemory.txt");
		} catch {};
		//System.Diagnostics.Process.Start("hex2bin","output\\"+FuenteSinExtension+".ihx");
		process = System.Diagnostics.Process.Start("c:\\windows\\system32\\cmd.exe","/c hex2bin output\\"+FuenteSinExtension+".ihx >>output\\HighMemory.txt");
		while (!process.HasExited)
			{
			//update UI
			}            
		
		if (File.Exists("output\\HighMemory.txt")) {
			Console.WriteLine("HighMemory.txt found................OK");
		}
		else {
			Console.WriteLine("ERROR: Cant create file .\\output\\HighMemory.txt with hex2bin.");
			Console.ReadLine();
			Environment.Exit(1);				
		}
		
		// averiguar salida bin memoria alta
		string miMemoriaAlta=""; int miMemoriaAltaEntero=0;
		
		string FileToRead = @".\\output\\HighMemory.txt";
		using (StreamReader ReaderObject = new StreamReader(FileToRead))
		{
			string Line;
			// ReaderObject reads a single line, stores it in Line string variable and then displays it on console
			while((Line = ReaderObject.ReadLine()) != null)
			{
				//'Console.WriteLine(Line);
				if (Line.Contains("Highest address = ")) {
				miMemoriaAlta=Line.Substring(18,8);
				Console.WriteLine("Found Hex OF high memory: .................... OK = "+miMemoriaAlta+"\r");
				miMemoriaAltaEntero = Convert.ToInt32(miMemoriaAlta, 16);
				Console.WriteLine("Convert to integer High Memory: .................... OK = "+miMemoriaAltaEntero+"\r");
				}
			}
		}
		if (miMemoriaAltaEntero>Empieza8bpInt-1) {
			Console.WriteLine("ERROR: High Memory "+miMemoriaAlta + "("+Convert.ToString(miMemoriaAltaEntero,10)+" exceeds the limit of "+Convert.ToString(Empieza8bpInt-1,10)+" where 8bp is located");
			Console.ReadLine();
			Environment.Exit(1);
		}
		else
			Console.WriteLine("HIgh Memory below LImit "+Empieza8bpString+" ............. OK\r");
		
		Console.WriteLine("Creating DSK FILE "+FuenteSinExtension+".DSK");

		//************************************************************************
		// Start the child process.
		Process p2 = new Process();
		// Redirect the output stream of the child process.
		p2.StartInfo.UseShellExecute = false;
		p2.StartInfo.RedirectStandardOutput = true;
		p2.StartInfo.FileName = "managedsk";
		string textin="managedsk "+ "-C -S\"output\\" + FuenteSinExtension + ".dsk\"";
		Debug.Print (textin); Console.Write	(textin);
		p2.StartInfo.Arguments = "\"-C -S\"output\\"+FuenteSinExtension+".dsk\"";
		
		p2.Start();
		// Do not wait for the child process to exit before
		// reading to the end of its redirected stream.
		// p.WaitForExit();
		// Read the output stream first and then wait.
		string output = p2.StandardOutput.ReadToEnd();
		p2.WaitForExit();
		Debug.Print(output);
		if (output.Contains("Erreur"))
		{
			Console.WriteLine("\n**************************************************************");
			Console.WriteLine("ERROR writing \"output\\" + FuenteSinExtension + ".dsk\". File in use? Please unlock right now, then retry!");
			Console.WriteLine("**************************************************************\n");
			Console.WriteLine("Press Enter\n");
			Console.ReadLine();
			Environment.Exit(1);
		}
		//************************************************************************


		//        process = System.Diagnostics.Process.Start("managedsk","-C -S"+(char)34+"output\\"+FuenteSinExtension+".dsk"+(char)34);
		//           while (!process.HasExited)
		//           {
		//           //update UI
		//           }            
		////managedsk -C -S"output\PK.dsk"			

		// insertar 8bp.bin en dsk
		Console.WriteLine("\nAdding 8BP to "+FuenteSinExtension+".DSK");		
		if (andepara8bpbin==1) 
		{ // 8bp.bin esta en asm
			suma="-L"+(char)34+"OUTPUT\\"+FuenteSinExtension+".DSK"+(char)34+" -i"+(char)34+"..\\ASM\\8BP.BIN"+(char)34+"/8BP.BIN/BIN/"+Empieza8bpString+" -S"+(char)34+"OUTPUT\\"+FuenteSinExtension+".DSK"+(char)34;
		}
		else
		{ // 8bp fin esta en local dir (donde está 8bphelper.exe)
			suma="-L"+(char)34+"OUTPUT\\"+FuenteSinExtension+".DSK"+(char)34+" -i"+(char)34+"8BP.BIN"+(char)34+"/8BP.BIN/BIN/"+Empieza8bpString+" -S"+(char)34+"OUTPUT\\"+FuenteSinExtension+".DSK"+(char)34;
		}
		Console.WriteLine(suma);
		process = System.Diagnostics.Process.Start("managedsk",suma);
			while (!process.HasExited)
			{
			//update UI
			}		

		Console.WriteLine("Adding binary to "+FuenteSinExtension+".DSK");		
		suma="-L"+(char)34+"OUTPUT\\"+FuenteSinExtension+".DSK"+(char)34+" -i"+(char)34+"OUTPUT\\"+FuenteSinExtension+".BIN"+(char)34+"/"+FuenteSinExtension+".BIN/BIN/"+Convert.ToString(memoriaStart, 10)+" -S"+(char)34+"OUTPUT\\"+FuenteSinExtension+".DSK"+(char)34;
		Console.WriteLine(suma);
		process = System.Diagnostics.Process.Start("managedsk",suma);
			while (!process.HasExited)
			{
			//update UI
			}
		
		//managedsk -L"output\\PK.dsk" -I"output\PK.bin"/PK.BIN/BIN/20000 -S"output\PK.dsk"		
		
		Console.WriteLine(     "Searching loader_base..................\r");
		if (File.Exists(".\\loader_base.bas")) {
			Console.WriteLine(".\\Found loader_base.bas .................. OK");
		}
		else {
			Console.WriteLine("loader_base Not Found. Creating a new one..  ");
				destFile = new System.IO.StreamWriter("loader_base.BAS");
				destFile.WriteLine("100 mode 1");
				destFile.WriteLine("110 rem reservada 1");
				destFile.WriteLine("120 rem reservada 2");
				destFile.WriteLine("130 rem reservada 3");
                destFile.WriteLine("200 memory %11%");
                destFile.WriteLine("220 PRINT\"Loading 8BP\"");
                destFile.WriteLine("240 LOAD"+(char)34+"8bp.bin"+(char)34);
                destFile.WriteLine("260 PRINT\"Loading %22%\"");
                destFile.WriteLine("280 LOAD"+(char)34+"%22%.BIN"+(char)34);
				destFile.Flush(); destFile.Close();
		}				
		
		// COMPROBAR si pasas memoria alta
			
		// CONSEGUIR DIRECCION MAIN
		string miMainCadena=""; int miMainEntero=0;
		FileToRead = @".\\output\\"+FuenteSinExtension+".map";
		using (StreamReader ReaderObject = new StreamReader(FileToRead))
		{
			string Line;
			// ReaderObject reads a single line, stores it in Line string variable and then displays it on console
			while((Line = ReaderObject.ReadLine()) != null)
			{
				//'Console.WriteLine(Line);
				if (Line.Contains("_main")) {
				//Console.WriteLine("Encontrado _main en línea: ["+Line+"]\r");
				miMainCadena=Line.Substring(6,7);
				Console.WriteLine("Found Hex OF _main: .................... OK = "+miMainCadena+"\r");
				miMainEntero = Convert.ToInt32(miMainCadena, 16);
				Console.WriteLine("Convert _main address to integer: ...... OK = "+miMainEntero+"\r");
				}
			}
		}
		
			// GENERAR LOADER con nombre bin correcto
		System.IO.StreamReader sourceFile = new System.IO.StreamReader("loader_base.bas");
		destFile = new System.IO.StreamWriter("output\\"+FuenteSinExtension+".BAS");
		string line ="";
		while ((line = sourceFile.ReadLine()) != null)
		{
			line=line.Replace("%11%",Convert.ToString(memoriaStart-1, 10));
			line=line.Replace("%22%",FuenteSinExtension);
			destFile.WriteLine(line);
		}
		destFile.WriteLine("900 'call &"+miMainCadena);
		destFile.WriteLine("1000 CALL "+miMainEntero);
		destFile.Write((char)26);
		destFile.Flush(); destFile.Close();
		sourceFile.Close();

		if (Pantalla!="") 
		{

			// añadimos a dsk la pantalla si ha venido como argumento
			Console.WriteLine("Adding "+Pantalla+" to "+FuenteSinExtension+".DSK (with Managedsk). Now, you can modify loader_base.bas to load "+Pantalla);
			suma="-L"+(char)34+"output\\"+FuenteSinExtension+".dsk"+(char)34+" -a"+Pantalla+" -S"+(char)34+"output\\"+FuenteSinExtension+".dsk"+(char)34;
			Console.WriteLine(suma);
			process = System.Diagnostics.Process.Start("managedsk",suma);			
			while (!process.HasExited)
			{
			//update UI
			}						
		}

		// localizamos archivo baseload.bas (loader del juego donde insertaremos la linea con el call &xxxx
		Console.WriteLine("Adding loader to "+FuenteSinExtension+".DSK");		
		suma="-L"+(char)34+"output\\"+FuenteSinExtension+".dsk"+(char)34+" -i"+(char)34+"OUTPUT\\"+FuenteSinExtension+".BAS"+(char)34+"/"+FuenteSinExtension+".BAS/ASCII -S"+(char)34+"output\\"+FuenteSinExtension+".dsk"+(char)34;
		Console.WriteLine(suma);
		process = System.Diagnostics.Process.Start("managedsk",suma);			
			while (!process.HasExited)
			{
			//update UI
			}
		Console.WriteLine("          ********** SUMMARY *************");
		Console.WriteLine("                  Path: "+path);
		Console.WriteLine("                Source: "+Fuente);
		Console.WriteLine("             SDCC path: "+rutaSDCC);
		Console.WriteLine("     8bp Assembly type: "+EnsamblajeTipo);
		Console.WriteLine("      8BP Memory Start: "+Empieza8bpString);
		Console.WriteLine("              8BP Size: "+Longitud8bpString);
		Console.WriteLine("     User Memory Start: "+memoriaStart.ToString() );
		Console.WriteLine("            Memory End: hex="+miMemoriaAlta+", dec="+miMemoriaAltaEntero.ToString() );
		Console.WriteLine("        main() address: hex="+miMainCadena+", dec="+miMainEntero.ToString() );
		Console.WriteLine("          8BP.BIN path: "+andepara8bpbinString);
		Console.WriteLine("          ********************************");
		Console.WriteLine("Check final "+FuenteSinExtension+".DSK created!");
		if (RecordatorioCompilar)
		{
			Console.WriteLine("***********************************");
			Console.WriteLine("Remember! With Sprites updated :-)");
			Console.WriteLine("***********************************");
		}


		//Console.Write("Type a number, and then press Enter: ");
		//numInput1 = Console.ReadLine();
		if (Ejecutalo)
		{
			textin = ".\\output\\" + FuenteSinExtension + ".dsk";

			//https://stackoverflow.com/questions/4467458/reading-a-registry-key-in-c-sharp
			//HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\FileExts\[EXT]\UserChoice\ProgId

			string ProgramaAsociado="";
			string regKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\FileExts\.dsk\UserChoice";
				using (Microsoft.Win32.RegistryKey uninstallKey = Registry.CurrentUser.OpenSubKey(regKey))
				{
					if (uninstallKey != null)
					{

						string[] productKeys = uninstallKey.GetValueNames();
						foreach ( string keyName in productKeys)
						{
							if (keyName == "ProgId" )
							{
								string App1=(Registry.CurrentUser.OpenSubKey(regKey).GetValue(keyName).ToString());
								ProgramaAsociado = Registry.ClassesRoot.OpenSubKey(App1.ToString() + "\\shell\\open\\command").GetValue(null).ToString();
								break; 
							}
						}
					}
					else
					{
					Console.Write("***************************************************************************\n");
					Console.Write("** Could not open emulator. Undefined DSK file association in the system **\n");
					Console.Write("***************************************************************************\n");
				}
				}
			if (!string.IsNullOrEmpty(ProgramaAsociado) )
			{
				ProgramaAsociado = ProgramaAsociado.Replace("\"%1\"", "").Trim();
				
				Console.WriteLine("Launching dsk associated program: " + ProgramaAsociado+" " + "output\\" + FuenteSinExtension + ".dsk");
				process = System.Diagnostics.Process.Start(ProgramaAsociado.Replace("\"%1\"",""), "output\\" + FuenteSinExtension + ".dsk");
				//while (!process.HasExited)
				{
					//update UI
				}
			}
		}
		Console.WriteLine("Press ENTER");
		while (Console.ReadKey().Key != ConsoleKey.Enter) { }
		Environment.Exit(0);
	}
	}
}
