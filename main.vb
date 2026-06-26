Imports System.IO
Imports System.Net
Imports System.Net.Http
Imports System.Runtime.CompilerServices
Imports System.Runtime.Remoting.Channels
Imports System.Runtime.Remoting.Messaging
Imports MySqlConnector.Protocol
Imports Newtonsoft.Json

Module main
    Private _dbLocal As eleantec.Library.database
    Private _dbSAGE As eleantec.Library.database
    Private _appConfig As eleantec.Library.Utilities.appConfiguration
    Private _databaseCfgLocal As eleantec.Library.Utilities.DatabaseStructure
    Private _databaseCfgRemote As eleantec.Library.Utilities.DatabaseStructure


    Private Const _urlSync As String = "https://data.eleantec.local/_tasks/"

    Sub Main(ByVal args() As String)
        If args.Length <> 1 Then Return

        Dim dtStarNow As DateTime = Now
        eleantec.Library.Utilities.Write2EventLog("Inicio de la aplicación para " & args(0).ToString.ToUpper, TraceEventType.Information)
        ServicePointManager.ServerCertificateValidationCallback = AddressOf AcceptAllCertifications

        'Console.WriteLine("Pass. " & eleantec.Library.Utilities.Crypt("5252Nov2315"))


        '
        '   Evito la ejecución de múltiples instancias de la aplicación
        '
        If Process.GetProcesses().Count(Function(p) p.ProcessName.Contains(My.Application.Info.AssemblyName)) > 1 Then
            eleantec.Library.Utilities.Write2EventLog("Ya hay una instancia de la aplicación en ejecución. Finalizando esta nueva instancia...")
            Return
        End If


        '
        ' Cargo la configuración de la conexión a la Base de Datos y otras configuraciones
        '
        If Not ReadConfiguration() Then Return

        'Console.WriteLine("Configuración cargada correctamente >>> " & eleantec.Library.Utilities.Decrypt("BrvwaNe8enLJ6rh/qfR91g==") & " >> " & eleantec.Library.Utilities.Crypt("5252Nov2315"))


        '
        '   Conecto con la base de datos local
        '
        _dbLocal = New eleantec.Library.database()
        _dbLocal.databaseCfg = _databaseCfgLocal
        If Not _dbLocal.Connect() Then
            eleantec.Library.Utilities.Write2EventLog("Error al conectar con la base de datos local: " & _dbLocal.lastError, TraceEventType.Error)
            Exit Sub
        End If


        '
        '   Conecto con la base de datos remota (SAGE) > lydCGIX2HgxZA8gYf9dosg==
        '
        _dbSAGE = New eleantec.Library.database()
        _dbSAGE.databaseCfg = _databaseCfgRemote
        If Not _dbSAGE.Connect(False) Then
            eleantec.Library.Utilities.Write2EventLog("Error al conectar con la base de datos local: " & _dbSAGE.lastError, TraceEventType.Error)
            _dbLocal.dispose()
            _dbLocal = Nothing
            Exit Sub
        End If


        '
        '   Cargo las configuraciones de la base de datos que me interesen
        '
        If Not readDatabaseConfiguration() Then Return


        '
        '   Proceso de subida y descarga de datos, que se ejecuta siempre independientemente de si hay rotura o no, para mantener los datos actualizados. El proceso de actualización solo se ejecutará en caso de rotura, pero la subida y descarga de datos se ejecutará siempre para mantener los datos actualizados.
        '
        uploadDataALL()
        downloadDataALL()

        reportPrint()




        '
        '   Caso de rotura (cambio) de día o de equipo: se ejecuta el proceso de actualización
        '
        If _appConfig.isBreak Then
            eleantec.Library.Utilities.Write2EventLog("Rotura detectada: " & If(_appConfig.appCurrentDate <> Now.ToString("yyyy-MM-dd"), "Cambio de día", "Cambio de equipo") & ". Ejecutando proceso de actualización...", TraceEventType.Information)
            '
            '   Aquí se ejecutaría el proceso de actualización, que podría ser una función o clase aparte dependiendo de la complejidad del mismo
            '
            Try
                '
                '   Realizamos copia de seguridad del fichero de configuración actual 
                '
                If IO.File.Exists(eleantec.Library.Utilities.fileConfiguration.Replace(".xml", ".bak")) Then
                    IO.File.Delete(eleantec.Library.Utilities.fileConfiguration.Replace(".xml", ".bak"))
                    IO.File.Copy(eleantec.Library.Utilities.fileConfiguration, eleantec.Library.Utilities.fileConfiguration.Replace(".xml", ".bak"))
                End If
            Catch ex As Exception
            End Try
        End If



        '
        '   Limpieza previa a la finalización de la aplicación
        '
        Try
            _dbLocal.dispose()
            _dbLocal = Nothing
        Catch ex As Exception
        End Try
        Try
            _dbSAGE.dispose()
            _dbSAGE = Nothing
        Catch ex As Exception
        End Try


        '
        '   Finalizo escribiendo la fecha de ultima ejecucion y notificandolo al servidor
        '
        Dim span As TimeSpan = Now - dtStarNow
        eleantec.Library.Utilities.SetXMLValue("appConfig", "appLastUpdate", Now.ToString("yyyy-MM-dd HH:mm:ss"))
        sayEndAll2server()
        Console.WriteLine("Proceso finalizado en " & ((span.Minutes * 60) + span.Seconds) & " segundos.")
        eleantec.Library.Utilities.Write2EventLog("Finalizando la aplicación en " & ((span.Minutes * 60) + span.Seconds) & "s a las " & Now.ToString("yyyy-MM-dd HH:mm:ss") & vbCrLf & vbCrLf, TraceEventType.Information)
    End Sub


#Region "Lectura de Configuraciones"
    Private Function ReadConfiguration() As Boolean
        Try
            '
            '   Cargo configuraciones Generales de la aplicación    
            '
            With _appConfig
                .appComputerName = eleantec.Library.Utilities.GetXMLValue("appConfig", "appComputerName", My.Computer.Name)
                .appCurrentDate = eleantec.Library.Utilities.GetXMLValue("appConfig", "appCurrentDate", DateTime.Now.AddDays(-1).ToString("yyyy-MM-dd"))
                .appLastUpdate = eleantec.Library.Utilities.GetXMLValue("appConfig", "appLastUpdate", DateTime.Now.AddDays(-1).ToString("yyyy-MM-dd HH:mm:ss"))
                .token = eleantec.Library.Utilities.GetXMLValue("appConfig", "token", "")

                .printerOrders = eleantec.Library.Utilities.GetXMLValue("appConfig", "printerOrders", "Printer")

                If .appCurrentDate <> Now.ToString("yyyy-MM-dd") Then
                    '
                    '   Caso de rotura (cambio) de día: se actualiza la fecha de última actualización y se marca la variable de rotura para que se ejecute el proceso
                    '
                    .appCurrentDate = Now.ToString("yyyy-MM-dd")
                    .isBreak = True
                    eleantec.Library.Utilities.SetXMLValue("appConfig", "appCurrentDate", .appCurrentDate.ToString("yyyy-MM-dd"))
                End If
                If .appComputerName <> My.Computer.Name Then
                    '
                    '   Caso de rotura (cambio) de equipo: se actualiza el nombre del equipo y se marca la variable de rotura para que se ejecute el proceso
                    '
                    .appComputerName = My.Computer.Name
                    .isBreak = True
                    eleantec.Library.Utilities.SetXMLValue("appConfig", "appComputerName", .appComputerName)
                End If
            End With


            '
            '   Cargo los datos de configuración de la base de datos local y la remota (SAGE)
            '
            With _databaseCfgLocal
                .host = eleantec.Library.Utilities.GetXMLValue("MySQL", "host", "localhost")
                .port = Convert.ToInt32(eleantec.Library.Utilities.GetXMLValue("MySQL", "port", "3306"))
                .name = eleantec.Library.Utilities.GetXMLValue("MySQL", "name", "localhost")
                .user = eleantec.Library.Utilities.GetXMLValue("MySQL", "user", "root")
                .pass = eleantec.Library.Utilities.Decrypt(eleantec.Library.Utilities.GetXMLValue("MySQL", "pass", eleantec.Library.Utilities.Crypt("eleantec")))
                .disabled = (eleantec.Library.Utilities.GetXMLValue("MySQL", "disabled", "0") = 1)
                .type = Library.Utilities.DatabaseType.MySQL
            End With

            With _databaseCfgRemote
                .host = eleantec.Library.Utilities.GetXMLValue("SQLServer", "host", "localhost")
                .port = Convert.ToInt32(eleantec.Library.Utilities.GetXMLValue("SQLServer", "port", "1433"))
                .name = eleantec.Library.Utilities.GetXMLValue("SQLServer", "name", "localhost")
                .user = eleantec.Library.Utilities.GetXMLValue("SQLServer", "user", "root")
                .pass = eleantec.Library.Utilities.Decrypt(eleantec.Library.Utilities.GetXMLValue("SQLServer", "pass", eleantec.Library.Utilities.Crypt("eleantec")))
                .disabled = (eleantec.Library.Utilities.GetXMLValue("SQLServer", "disabled", "0") = 1)
                .type = Library.Utilities.DatabaseType.SQLServer
            End With
        Catch ex As Exception
            eleantec.Library.Utilities.Write2EventLog("Error al cargar la configuración: " & ex.Message, TraceEventType.Error)
            Return False
        End Try
        Return True
    End Function


    Private Function readDatabaseConfiguration() As Boolean
        Dim sql As String = ""
        Try
            Try
                '
                '   Realizo limpieza de la base de datos local
                '
                sql = "DELETE FROM `tmporders`"
                _dbLocal.query(sql)

                sql = "DELETE FROM `tmporders_lines`"
                _dbLocal.query(sql)

                System.Threading.Thread.Sleep(350)
            Catch ex As Exception
            End Try
        Catch ex As Exception
            eleantec.Library.Utilities.Write2EventLog("Error al cargar la configuraciones desde la base de datos: " & ex.Message, TraceEventType.Error)
            Return False
        End Try
        Return True
    End Function

#End Region


#Region "Procedimientos"

    Private Function uploadDataALL() As Boolean
        Dim sql As String = "", content As String = ""
        Dim nOTs As Integer = 0, nClientes As Integer = 0, nWorkers As Integer = 0, nAlbaranes As Integer = 0
        Dim nOTsOK As Integer = 0, nClientesOK As Integer = 0, nWorkersOK As Integer = 0, nAlbaranesOK As Integer = 0
        Dim dt As DataTable = Nothing, dtLines As DataTable = Nothing
        Dim json As StringContent = Nothing, jsonArray As StringContent() = Nothing
        Dim jsonLines As Newtonsoft.Json.Linq.JArray = Nothing

        Dim client As HttpClient = Nothing
        Dim response As HttpResponseMessage = Nothing
        Dim payload As String = ""
        Dim responseJSON As Newtonsoft.Json.Linq.JObject = Nothing

        client = New HttpClient() : client.Timeout = New TimeSpan(TimeSpan.TicksPerSecond * 5) : client.BaseAddress = New Uri(_urlSync) : client.DefaultRequestHeaders.Add("Accept", "application/json")
        'client.DefaultRequestHeaders.Add("Authorization", "Bearer " & _appConfig.token)
        client.DefaultRequestHeaders.Add("Token", _appConfig.token)

        'GoTo Albaranes
        'GoTo Trabajadores


        '
        '   OrdenesTrabajo
        '
        sql = "SELECT * FROM [IT_ORDENESTRABAJO] WHERE LEN([IT_CONCEPTO])>0 AND [dtUpdate] >= '" & _appConfig.appLastUpdate.ToString("yyyy-MM-dd HH:mm:ss") & ".000' ORDER BY [CodigoEmpresa] ASC, [IT_NUMEROOT] ASC"
        dt = _dbSAGE.getData(sql) : nOTs = 0 : nOTsOK = 0
        If IsNothing(dt) Then Return False
        For Each row As DataRow In dt.Rows
            nOTs += 1
            json = New StringContent(
                                        "{ " &
                                            """CodigoEmpresa"": """ & getValue(row("CodigoEmpresa")) & """, " +
                                            """NumeroOT"": """ & getValue(row("IT_NUMEROOT")) & """, " +
                                            """FechaInicioOT"": """ & getValue(row("IT_FECHAINICIOOT")) & """, " +
                                            """FechaFinOT"": """ & getValue(row("IT_FECHAFINOT")) & """, " +
                                            """Tipo"": """ & getValue(row("IT_TIPO")) & """, " +
                                            """CodigoJefeOrden"": """ & getValue(row("IT_CODIGOJEFEORDEN")) & """, " +
                                            """CodigoCliente"": """ & getValue(row("CodigoCliente")) & """, " +
                                            """RazonSocial"": """ & getValue(row("RazonSocial")) & """, " +
                                            """Domicilio"": """ & getValue(row("Domicilio")) & """, " +
                                            """CodigoPostal"": """ & getValue(row("CodigoPostal")) & """, " +
                                            """CodigoMunicipio"": """ & getValue(row("CodigoMunicipio")) & """, " +
                                            """Municipio"": """ & getValue(row("Municipio")) & """, " +
                                            """CodigoProvincia"": """ & getValue(row("CodigoProvincia")) & """, " +
                                            """Provincia"": """ & getValue(row("Provincia")) & """, " +
                                            """Telefono"": """ & getValue(row("Telefono")) & """, " +
                                            """Telefono2"": """ & getValue(row("Telefono2")) & """, " +
                                            """Comentarios"": """ & getValue(row("Comentarios")) & """, " +
                                            """FechaPrevistaFinal"": """ & getValue(row("IT_FECHAPREVISTAFINAL")) & """, " +
                                            """Nombre1"": """ & getValue(row("Nombre1")) & """, " +
                                            """CodigoProyecto"": """ & getValue(row("CodigoProyecto")) & """, " +
                                            """Proyecto"": """ & getValue(row("Proyecto")) & """, " +
                                            """PedidoCuadro"": """ & IIf((row("IT_PEDIDOCUADRO").ToString = -1), 1, 0) & """, " +
                                            """PedidoInstalacion"": """ & IIf((row("IT_PEDIDOINSTALACION").ToString = -1), 1, 0) & """, " +
                                            """PedidoProgramacion"": """ & IIf((row("IT_PEDIDOPROGRAMACION").ToString = -1), 1, 0) & """, " +
                                            """Averia"": """ & IIf((row("IT_AVERIA").ToString = -1), 1, 0) & """, " +
                                            """ProtocoloEnsayo"": """ & IIf((row("IT_PROTOCOLOENSAYO").ToString = -1), 1, 0) & """, " +
                                            """OrigenCliente"": """ & IIf((row("IT_ORIGENCLIENTE").ToString = -1), 1, 0) & """, " +
                                            """Origen"": """ & IIf((row("IT_ORIGEN").ToString = -1), 1, 0) & """, " +
                                            """OTFacturable"": """ & IIf((row("IT_OTFACTURABLE").ToString = -1), 1, 0) & """, " +
                                            """Concepto"": """ & getValue(row("IT_CONCEPTO")) & """, " +
                                            """EjercicioOferta"": """ & getValue(row("EjercicioOferta")) & """, " +
                                            """SerieOferta"": """ & getValue(row("SerieOferta")) & """, " +
                                            """NumeroOferta"": """ & getValue(row("NumeroOferta")) & """, " +
                                            """Revisado"": """ & IIf((row("Revisado").ToString = -1), 1, 0) & """, " +
                                            """OperarioCreaOrden"": """ & getValue(row("IT_OPERARIOCREAORDEN")) & """, " +
                                            """NumeroCuadro"": """ & getValue(row("IT_NUMEROCUADRO")) & """, " +
                                            """UbicacionObra"": """ & getValue(row("IT_UBICACIONOBRA")) & """, " +
                                            """StatusFacturado"": """ & IIf((row("StatusFacturado").ToString = -1), 1, 0) & """, " +
                                            """FacturaAsociada"": """ & getValue(row("IT_FACTURAASOCIADA")) & """, " +
                                            """Departamento"": """ & getValue(row("IT_DEPARTAMENTO")) & """, " +
                                            """FechaEntregaOperario"": """ & getValue(row("IT_FECHAENTREGAOPERARIO")) & """, " +
                                            """EjercicioFactura"": """ & getValue(row("EjercicioFactura")) & """, " +
                                            """SerieFactura"": """ & getValue(row("SerieFactura")) & """, " +
                                            """NumeroFactura"": """ & getValue(row("NumeroFactura")) & """, " +
                                            """CodigoMunicipioCT"": """ & IIf(row("CodigoMunicipioCT").ToString.Length = 0, getValue(row("CodigoPostal")), getValue(row("CodigoMunicipioCT"))) & """, " +
                                            """EstadoEjecucion"": """ & getValue(row("IT_ESTADOEJECUCION")) & """, " +
                                            """TecnicoAsignado"": """ & getValue(row("IT_TECNICOASIGNADO")) & """, " +
                                            """TecnicoAsignadoNombre"": """ & getValue(row("IT_TECNICOASIGNADONOMBRE")) & """, " +
                                            """ObservacionesInternas"": """ & getValue(row("IT_OBSERVACIONESINTERNAS")) & """, " +
                                            """OperarioRevisa"": """ & getValue(row("IT_OPERARIOREVISA")) & """, " +
                                            """OperarioRevisaNombre"": """ & getValue(row("IT_OPERARIOREVISANOMBRE")) & """, " +
                                            """OperarioCierra"": """ & getValue(row("IT_OPERARIOCIERRA")) & """, " +
                                            """OperarioCierraNombre"": """ & getValue(row("IT_OPERARIOCIERRANOMBRE")) & """, " +
                                            """OrdenCerrada"": """ & IIf(row("IT_ORDENCERRADA") = -1, 1, 0) & """, " +
                                            """ForzarOrden"": """ & IIf(row("IT_FORZARORDEN") = -1, 1, 0) & """, " +
                                            """NumeroAveria"": """ & getValue(row("IT_NUMEROAVERIA")) & """, " +
                                            """ComentariosOT"": """ & getValue(row("IT_COMENTARIOS_OT")) & """, " +
                                            """SuPedido"": """ & getValue(row("SuPedido")) & """, " +
                                            """OperarioSolicitaOT"": """ & getValue(row("IT_OPERARIOSOLICITAOT")) & """, " +
                                            """OperarioSolicitaOTNombre"": """ & getValue(row("IT_OPERARIOSOLICITAOTNOMBRE")) & """, " +
                                            """OperarioEjecutaOT"": """ & getValue(row("IT_OPERARIOEJECUTAOT")) & """, " +
                                            """OperarioEjecutaOTNombre"": """ & getValue(row("IT_OPERARIOEJECUTAOTNOMBRE")) & """, " +
                                            """OperarioCreaOrdenNombre"": """ & getValue(row("IT_OPERARIOCREAORDENNOMBRE")) & """, " +
                                            """StatusOT"": """ & getValue(row("StatusOT")) & """, " +
                                            """FechaResolucion"": """ & getValue(row("IT_FECHARESOLUCION")) & """, " +
                                            """FechaCierreOT"": """ & getValue(row("IT_FECHACIERREOT")) & """, " +
                                            """OperarioFinaliza"": """ & getValue(row("IT_OPERARIOFINALIZA")) & """, " +
                                            """OperarioFinalizaNombre"": """ & getValue(row("IT_OPERARIOFINALIZANOMBRE")) & """, " +
                                            """FechaRevision"": """ & getValue(row("IT_FECHAREVISION")) & """, " +
                                            """NumeroOTRelacionado"": """ & getValue(row("IT_NUMEROOTRELACIONADO")) & """, " +
                                            """OperarioResuelveOT"": """ & getValue(row("IT_OPERARIORESUELVEOT")) & """, " +
                                            """OperarioResuelveOTNombre"": """ & getValue(row("IT_OPERARIORESUELVENOMBRE")) & """, " +
                                            """dtUpdate"": """ & getValue(row("dtUpdate")) & """" +
                                        "}" _
                                        , System.Text.Encoding.UTF8, "application/json"
                                     )

            Try
                Console.WriteLine(json.ReadAsStringAsync().Result)

                response = client.PostAsync(_urlSync & "upload-workorders", json).Result
                If response.StatusCode <> 200 Then Continue For

                payload = response.Content.ReadAsStringAsync().Result
                responseJSON = Newtonsoft.Json.Linq.JObject.Parse(payload)
                If Convert.ToBoolean(responseJSON.Item("isOK")) Then nOTsOK += 1
            Catch ex As Exception
                eleantec.Library.Utilities.Write2EventLog("Error al subir el cliente " & row("CodigoCliente").ToString & " " & row("RazonSocial").ToString & ": " & ex.Message, TraceEventType.Error)
            End Try
            Console.WriteLine("OrdenTrabajo: " & row("IT_NUMEROOT").ToString)
        Next
        dt.Dispose() : dt = Nothing

        'Console.WriteLine(vbCrLf & Space(120).Replace(" ", "-") & vbCrLf)


Clientes:
        '
        '   Clientes
        '
        sql = "SELECT  " +
                    "C.[CodigoEmpresa], C.[CodigoCliente], C.[RazonSocial], C.[Nombre1], C.[CifDni], C.[Domicilio], C.[CodigoPostal], C.[CodigoMunicipio], C.[Municipio], C.[CodigoProvincia], C.[Provincia], C.[DomicilioEnvio], C.[Telefono], C.[Telefono2], C.[FechaAlta], C.[BajaEmpresaLc], C.[FechaBajaLc], C.[EMail1], C.[EMail2],S.[sysFechaRegistro] " +
                "FROM [Clientes] C LEFT JOIN [Clientes_Sync] S ON C.[IdCliente]=S.[sysGuidRegistro] " +
                "WHERE C.[CodigoCategoriaCliente_] = 'CLI' AND S.[sysFechaRegistro] >= '" & _appConfig.appLastUpdate.ToString("yyyy-MM-dd HH:mm:ss") & ".000' " +
                "ORDER BY CONVERT(int, C.[CodigoCliente]) "
        dt = _dbSAGE.getData(sql) : nClientes = 0 : nClientesOK = 0
        For Each row As DataRow In dt.Rows
            nClientes += 1
            json = New StringContent(
                                        "{ " &
                                            """CodigoEmpresa"": """ & getValue(row("CodigoEmpresa")) & """, " +
                                            """CodigoCliente"": """ & getValue(row("CodigoCliente")) & """, " +
                                            """RazonSocial"": """ & getValue(row("RazonSocial")) & """, " +
                                            """CifDni"": """ & getValue(row("CifDni")) & """, " +
                                            """Domicilio"": """ & getValue(row("Domicilio")) & """, " +
                                            """CodigoPostal"": """ & getValue(row("CodigoPostal")) & """, " +
                                            """CodigoMunicipio"": """ & getValue(row("CodigoMunicipio")) & """, " +
                                            """Municipio"": """ & getValue(row("Municipio")) & """, " +
                                            """CodigoProvincia"": """ & getValue(row("CodigoProvincia")) & """, " +
                                            """Provincia"": """ & getValue(row("Provincia")) & """, " +
                                            """DomicilioEnvio"": """ & getValue(row("DomicilioEnvio")) & """, " +
                                            """Telefono"": """ & getValue(row("Telefono")) & """, " +
                                            """Telefono2"": """ & getValue(row("Telefono2")) & """, " +
                                            """Nombre1"": """ & getValue(row("Nombre1")) & """, " +
                                            """FechaAlta"": """ & getValue(row("FechaAlta")) & """, " +
                                            """BajaEmpresaLc"": """ & IIf((row("BajaEmpresaLc").ToString = -1), 1, 0) & """, " +
                                            """FechaBajaLc"": """ & getValue(row("FechaBajaLc")) & """, " +
                                            """EMail1"": """ & getValue(row("EMail1")) & """, " +
                                            """EMail2"": """ & getValue(row("EMail2")) & """, " +
                                            """dtUpdate"": """ & getValue(row("sysFechaRegistro")) & """" +
                                        "}" _
                                        , System.Text.Encoding.UTF8, "application/json"
                                     )

            Try
                response = client.PostAsync(_urlSync & "upload-customers", json).Result
                If response.StatusCode <> 200 Then Continue For

                payload = response.Content.ReadAsStringAsync().Result
                responseJSON = Newtonsoft.Json.Linq.JObject.Parse(payload)
                If Convert.ToBoolean(responseJSON.Item("isOK")) Then nWorkersOK += 1
            Catch ex As Exception
                eleantec.Library.Utilities.Write2EventLog("Error al subir el cliente " & row("CodigoCliente").ToString & " " & row("RazonSocial").ToString & ": " & ex.Message, TraceEventType.Error)
            End Try

            Console.WriteLine("Cliente: " & row("CodigoCliente").ToString & " " & row("RazonSocial").ToString)
        Next
        dt.Dispose() : dt = Nothing

        'Console.WriteLine(vbCrLf & Space(120).Replace(" ", "-") & vbCrLf)


Trabajadores:
        '
        '   Trabajadores
        '
        sql = "SELECT  " +
                    "C.[CodigoEmpresa], C.[CodigoCliente], C.[RazonSocial], C.[CifDni], C.[Domicilio], C.[CodigoPostal], C.[CodigoMunicipio], C.[Municipio], C.[CodigoProvincia], C.[Provincia], C.[DomicilioEnvio], C.[Telefono], C.[Telefono2], C.[FechaAlta], C.[BajaEmpresaLc], C.[FechaBajaLc], C.[EMail1], C.[EMail2], C.[IT_DEPARTAMENTO], S.[sysFechaRegistro] " +
                "FROM [Clientes] C LEFT JOIN [Clientes_Sync] S ON C.[IdCliente]=S.[sysGuidRegistro] " +
                "WHERE C.[CodigoCategoriaCliente_] = 'EMP' AND S.[sysFechaRegistro] >= '" & _appConfig.appLastUpdate.ToString("yyyy-MM-dd HH:mm:ss") & ".000' " +
                "ORDER BY CONVERT(int, C.[CodigoCliente]) "
        dt = _dbSAGE.getData(sql) : nWorkers = 0 : nWorkersOK = 0
        For Each row As DataRow In dt.Rows
            nWorkers += 1
            json = New StringContent(
                                        "{ " &
                                            """code"": """ & getValue(row("CodigoCliente")) & """, " +
                                            """name"": """ & getValue(row("RazonSocial")) & """, " +
                                            """pass"": """ & getUserPassword(row("CifDni")) & """, " +
                                            """disabled"": """ & IIf((row("BajaEmpresaLc").ToString = -1), 1, 0) & """, " +
                                            """section"": """ & getValue(row("IT_DEPARTAMENTO")) & """, " +
                                            """telephone1"": """ & getValue(row("Telefono")) & """, " +
                                            """telephone2"": """ & getValue(row("Telefono2")) & """, " +
                                            """email1"": """ & getValue(row("EMail1")) & """, " +
                                            """email2"": """ & getValue(row("EMail2")) & """, " +
                                            """dtUpdate"": """ & getValue(row("sysFechaRegistro")) & """" +
                                        "}" _
                                        , System.Text.Encoding.UTF8, "application/json"
                                     )

            Try
                response = client.PostAsync(_urlSync & "upload-workers", json).Result
                If response.StatusCode <> 200 Then Continue For

                payload = response.Content.ReadAsStringAsync().Result
                responseJSON = Newtonsoft.Json.Linq.JObject.Parse(payload)
                If Convert.ToBoolean(responseJSON.Item("isOK")) Then nWorkersOK += 1
            Catch ex As Exception
                eleantec.Library.Utilities.Write2EventLog("Error al subir el trabajador " & row("CodigoCliente").ToString & " " & row("RazonSocial").ToString & ": " & ex.Message, TraceEventType.Error)
            End Try


            Console.WriteLine("Trabajador: " & row("CodigoCliente").ToString & " " & row("RazonSocial").ToString)
        Next
        dt.Dispose() : dt = Nothing


        '
        '   Albaranes
        '
Albaranes:
        sql = "SELECT " +
                    "A.[CodigoEmpresa], A.[EjercicioAlbaran], A.[SerieAlbaran], A.[NumeroAlbaran], A.[FechaAlbaran], A.[CodigoCliente], A.[IT_NUMEROOT], A.[RefApp] " +
                "FROM [CabeceraAlbaranCliente] A " +
                "WHERE A.[EsApp] = -1 AND A.[dtUpdate] >= '" & _appConfig.appLastUpdate.ToString("yyyy-MM-dd HH:mm:ss") & ".000' "
        dt = _dbSAGE.getData(sql) : nAlbaranes = 0 : nAlbaranesOK = 0 : content = ""
        For Each row As DataRow In dt.Rows
            sql = "SELECT L.[CodigoArticulo], L.[DescripcionArticulo], L.[Unidades], L.[IDLineasAlbaranCliente] FROM [LineasAlbaranCliente] L WHERE L.[CodigoEmpresa] = " & row("CodigoEmpresa").ToString & " AND L.[EjercicioAlbaran] = " & row("EjercicioAlbaran").ToString & " AND L.[SerieAlbaran] = '" & row("SerieAlbaran").ToString & "' AND L.[NumeroAlbaran] = " & row("NumeroAlbaran").ToString
            dtLines = _dbSAGE.getData(sql)
            ReDim jsonArray(dtLines.Rows.Count - 1) : content = ""
            For i As Integer = 0 To dtLines.Rows.Count - 1
                content &= IIf(content.Length > 0, ",", "") & "" +
                             "{ " +
                                """CodigoArticulo"": """ & getValue(dtLines.Rows(i)("CodigoArticulo")) & """, " +
                                """IDLineasAlbaranCliente"": """ & getValue(dtLines.Rows(i)("IDLineasAlbaranCliente")) & """, " +
                                """DescripcionArticulo"": """ & getValue(dtLines.Rows(i)("DescripcionArticulo")) & """, " +
                                """Unidades"": """ & getValue(dtLines.Rows(i)("Unidades")) & """ " +
                             "}"
            Next

            json = New StringContent(
                                        "{ " +
                                            """CodigoEmpresa"": """ & getValue(row("CodigoEmpresa")) & """, " +
                                            """EjercicioAlbaran"": """ & getValue(row("EjercicioAlbaran")) & """, " +
                                            """SerieAlbaran"": """ & getValue(row("SerieAlbaran")) & """, " +
                                            """NumeroAlbaran"": """ & getValue(row("NumeroAlbaran")) & """, " +
                                            """FechaAlbaran"": """ & getValue(row("FechaAlbaran")) & """, " +
                                            """CodigoCliente"": """ & getValue(row("CodigoCliente")) & """, " +
                                            """NumeroOT"": """ & getValue(row("IT_NUMEROOT")) & """, " +
                                            """RefApp"": """ & getValue(row("RefApp")).ToUpper & """, " +
                                            """LineasAlbaran"": [" & content & "]" +
                                        "}" _
                                        , System.Text.Encoding.UTF8, "application/json"
                                     )

            Try
                response = client.PostAsync(_urlSync & "upload-albaranes", json).Result
                If response.StatusCode <> 200 Then Continue For
                payload = response.Content.ReadAsStringAsync().Result
                responseJSON = Newtonsoft.Json.Linq.JObject.Parse(payload)
                If Convert.ToBoolean(responseJSON.Item("isOK")) Then nAlbaranesOK += 1
            Catch ex As Exception
                eleantec.Library.Utilities.Write2EventLog("Error al subir el albarán " & row("EjercicioAlbaran").ToString & "/" & row("SerieAlbaran").ToString & "/" & row("NumeroAlbaran").ToString & ": " & ex.Message, TraceEventType.Error)
            End Try
            Console.WriteLine("Albarán: " & row("EjercicioAlbaran").ToString & "/" & row("SerieAlbaran").ToString & "/" & row("NumeroAlbaran").ToString & " >> " & row("RefApp"))

            nAlbaranes += 1
            dtLines.Dispose() : dtLines = Nothing
        Next
        dt.Dispose() : dt = Nothing


        If (nOTs + nClientes + nWorkers + nAlbaranes) > 0 Then
            Dim str As String = ""
            If nOTs > 0 Then str &= "OrdenesTrabajo: " & nOTsOK & "/" & nOTs & ", "
            If nClientes > 0 Then str &= "Clientes: " & nClientesOK & "/" & nClientes & ", "
            If nWorkers > 0 Then str &= "Trabajadores: " & nWorkersOK & "/" & nWorkers & ", "
            If nAlbaranes > 0 Then str &= "Albaranes: " & nAlbaranesOK & "/" & nAlbaranes & ", "

            eleantec.Library.Utilities.Write2EventLog("Procesamiento terminado: " & str & " OK", TraceEventType.Information)
        End If

        Return True
    End Function


    Private Function downloadDataALL() As Boolean
        Dim sql As String = "", str As String = "", data As DataTable = Nothing
        Dim nPartes As Integer = 0, nPartesTot As Integer = 0, nHorasND As Double = 0, nHorasDesplazamiento As Double = 0, nHorasNDTemp As Double = 0, nHorasDesplazamientoTemp As Double = 0, nHorasExtraTemp As Double = 0, nDietaMedia As Integer = 0, nDietaCompleta As Integer = 0

        Dim json As StringContent = Nothing

        Dim client As HttpClient = Nothing
        Dim response As HttpResponseMessage = Nothing
        Dim payload As String = ""
        Dim responseJSON As Newtonsoft.Json.Linq.JObject = Nothing, responseJSONOK As Newtonsoft.Json.Linq.JObject = Nothing
        Dim responseJSONWO As Newtonsoft.Json.Linq.JArray = Nothing, responseJSONUnique As Newtonsoft.Json.Linq.JArray = Nothing

        Dim whoOK() As Integer = {-1}

        client = New HttpClient() : client.Timeout = New TimeSpan(TimeSpan.TicksPerSecond * 5) : client.BaseAddress = New Uri(_urlSync) : client.DefaultRequestHeaders.Add("Accept", "application/json")
        client.DefaultRequestHeaders.Add("Token", _appConfig.token)

        Try
            '
            '   Solicito el listado de partes procesable
            '
            json = New StringContent("{ ""code"": """ & "52" & """}", System.Text.Encoding.UTF8, "application/json")
            response = client.PostAsync(_urlSync & "download-workparts", json).Result
            If response.StatusCode <> 200 Then Exit Try
            payload = response.Content.ReadAsStringAsync().Result
            responseJSON = Newtonsoft.Json.Linq.JObject.Parse(payload)
            If Not Convert.ToBoolean(responseJSON.Item("isOK")) Then Exit Try

            Console.WriteLine(responseJSON.Item("response").Item("workparts"))
            Console.WriteLine(responseJSON.Item("response").Item("unique"))
            responseJSONWO = responseJSON.Item("response").Item("workparts")
            responseJSONUnique = responseJSON.Item("response").Item("unique")


            '
            '   Proceso los partes de trabajo
            '
            For Each parte As Newtonsoft.Json.Linq.JObject In responseJSONWO
                nPartesTot += 1

                If Array.IndexOf(whoOK, Convert.ToInt32(parte.Item("whoOK").ToString)) < 0 Then
                    ReDim Preserve whoOK(whoOK.Length)
                    whoOK(whoOK.Length - 1) = Convert.ToInt32(parte.Item("whoOK").ToString)
                End If


                Try
                    '
                    '   Notifico que estoy procesando el parte de trabajo para que lo marque como en procesado y no me lo vuelva a enviar
                    '
                    json = New StringContent("{ ""partNo"": """ & parte.Item("partNo").ToString & """}", System.Text.Encoding.UTF8, "application/json")
                    response = client.PostAsync(_urlSync & "download-workpartOK", json).Result
                    If response.StatusCode <> 200 Then Continue For
                    payload = response.Content.ReadAsStringAsync().Result
                    responseJSON = Newtonsoft.Json.Linq.JObject.Parse(payload)
                    If Not Convert.ToBoolean(responseJSON.Item("isOK")) Then Continue For
                Catch ex As Exception
                    eleantec.Library.Utilities.Write2EventLog("errProcesando partNo: " & parte.Item("partNo").ToString & " - " & ex.Message, TraceEventType.Critical)
                    Continue For
                End Try


                Try
                    '
                    '   Agrego la cabecera del parte de trabajo a la base de datos local (si no existe ya) para su posterior procesamiento
                    '
                    sql = "INSERT INTO [IT_CABECERAPARTES] ([CodigoEmpresa], [Fecha], [Operario], [NombreOperario], [IT_DIAFESTIVO], [StatusParte], [CodigoCliente], [RazonSocial]) " +
                                                            "VALUES(" +
                                                                "'" & parte.Item("CodigoEmpresa").ToString & "'," +
                                                                "'" & parte.Item("Fecha").ToString & "'," +
                                                                "'" & parte.Item("Operario").ToString & "'," +
                                                                "'" & parte.Item("NombreOperario").ToString.ToUpper & "'," +
                                                                "'" & IIf(Convert.ToBoolean(parte.Item("EsFestivo").ToString), -1, 0) & "'," +
                                                                "'" & 0 & "'," +
                                                                "'" & parte.Item("Operario").ToString & "'," +
                                                                "'" & parte.Item("NombreOperario").ToString.ToUpper & "'" +
                                                            ")"
                    _dbSAGE.query(sql)
                Catch ex As Exception
                End Try

                '
                '   Proceso y ajusto los partes de trabajo
                '
                nHorasND = Convert.ToDouble(parte.Item("HorasND"))
                nHorasDesplazamiento = Convert.ToDouble(parte.Item("HorasDesplazamiento"))
                Do
                    '
                    '   Calculos para ajustar las horas de desplazamiento y no disponibilidad a las 8 horas máximas del parte de trabajo, generando partes de trabajo adicionales si es necesario
                    '
                    nDietaCompleta = 0 : nDietaMedia = 0
                    If nHorasDesplazamiento >= 8 Then
                        nHorasDesplazamientoTemp = 8
                        nHorasNDTemp = 0
                        nHorasDesplazamiento -= 8

                        nDietaCompleta = parte.Item("DietaCompleta").ToString
                        nDietaMedia = parte.Item("MediaDieta").ToString
                    ElseIf nHorasDesplazamiento > 0 Then
                        nHorasDesplazamientoTemp = nHorasDesplazamiento
                        nHorasDesplazamiento = 0
                        If nHorasDesplazamientoTemp + nHorasND >= 8 Then
                            nHorasNDTemp = 8 - nHorasDesplazamientoTemp
                            nHorasND -= nHorasNDTemp
                        Else
                            nHorasNDTemp = nHorasND
                            nHorasND = 0
                        End If
                    ElseIf nHorasDesplazamiento = 0 And nHorasND > 0 Then
                        nHorasDesplazamientoTemp = 0
                        If nHorasND >= 8 Then
                            nHorasNDTemp = 8
                            nHorasND -= 8
                        Else
                            nHorasNDTemp = nHorasND
                            nHorasND = 0
                        End If
                    End If

                    Try
                        str = parte.Item("ObservacionesParte").ToString.ToUpper.Replace("'", "")
                        If str.Length > 240 Then str = str.Substring(0, 240)

                        nHorasExtraTemp = 0
                        If Convert.ToBoolean(parte.Item("EsFestivo").ToString) Then
                            nHorasExtraTemp = nHorasND
                            nHorasND = 0
                        End If

                        '
                        '   Compruebo si el parte de trabajo ya existe (por si esta lanzado el proceso varias veces)
                        '
                        sql = "SELECT COUNT(*) AS nTot FROM [IT_ENTRADAPARTES] WHERE [CodigoEmpresa]='" & parte.Item("CodigoEmpresa").ToString & "' AND [Fecha]='" & parte.Item("Fecha").ToString & "' AND [Operario]='" & parte.Item("Operario").ToString & "' AND [IT_NUMEROOT]='" & parte.Item("NumeroOT").ToString.ToUpper & "' AND [IT_TOTALHORAS]='" & Math.Round(nHorasNDTemp + nHorasExtraTemp + nHorasDesplazamientoTemp, 2).ToString.Replace(",", ".") & "'"
                        data = _dbSAGE.getData(sql)
                        If data(0)("nTot") > 0 Then
                            _dbSAGE.closeDatatable(data)
                            Continue Do
                        End If
                        _dbSAGE.closeDatatable(data)


                        '
                        '   Agrego el parte de trabajo
                        '
                        sql = "INSERT INTO [IT_ENTRADAPARTES] ([CodigoEmpresa], [Fecha], [Operario], [CodigoCliente], [IT_NUMEROOT], [IT_DIETACOMPLETA], [IT_MEDIADIETA], [IT_KILOMETROS], [IT_HORASND], [IT_HORASEXTRA], [IT_HORASDESPLAZAMIENTO], [IT_TOTALHORAS], [IT_OBSERVACIONESPARTE]) " +
                                                            "VALUES(" +
                                                                "'" & parte.Item("CodigoEmpresa").ToString & "'," +
                                                                "'" & parte.Item("Fecha").ToString & "'," +
                                                                "'" & parte.Item("Operario").ToString & "'," +
                                                                "'" & parte.Item("Operario").ToString & "'," +
                                                                "'" & parte.Item("NumeroOT").ToString.ToUpper & "'," +
                                                                "'" & nDietaCompleta & "'," +
                                                                "'" & nDietaMedia & "'," +
                                                                "'" & parte.Item("Kilometros").ToString.ToString.Replace(",", ".") & "'," +
                                                                "'" & nHorasNDTemp.ToString.Replace(",", ".") & "'," +
                                                                "'" & nHorasExtraTemp.ToString.Replace(",", ".") & "'," +
                                                                "'" & nHorasDesplazamientoTemp.ToString.Replace(",", ".") & "'," +
                                                                "'" & Math.Round(nHorasNDTemp + nHorasExtraTemp + nHorasDesplazamientoTemp, 2).ToString.Replace(",", ".") & "'," +
                                                                "'" & str & "'" +
                                                            ")"
                        'Console.WriteLine(sql)

                        _dbSAGE.query(sql)


                    Catch ex As Exception
                        eleantec.Library.Utilities.Write2EventLog("errProcesando Parte: " & ex.Message, TraceEventType.Critical)
                    End Try

                    parte.Item("Kilometros") = 0
                    nPartes += 1
                Loop While nHorasND > 0 Or nHorasDesplazamiento > 0
            Next


            '
            '   Recalculo y actualizo las cabeceras totales de los partes de trabajo
            '
            For Each item As String In responseJSONUnique
                sql = "SELECT " +
                            "SUM(IT_HORASND) AS TotalHorasND, " +
                            "SUM(IT_HORASEXTRA) AS TotalHorasExtra, " +
                            "SUM(IT_TOTALHORAS) AS TotalHoras, " +
                            "SUM(IT_HORASDESPLAZAMIENTO) AS TotalHorasDesplazamiento, " +
                            "SUM(IT_KILOMETROS) AS TotalKilometros, " +
                            "SUM(IT_DIETACOMPLETA) AS TotalDietaCompleta, " +
                            "SUM(IT_MEDIADIETA) AS TotalMediaDieta, " +
                            "SUM(IT_GASTOSGENERALES) AS TotalGastosGenerales " +
                        "FROM [IT_ENTRADAPARTES] " +
                            "WHERE " +
                        "CodigoEmpresa='" & item.Split(";")(0) & "' AND Fecha='" & item.Split(";")(1) & "' AND Operario='" & item.Split(";")(2) & "'"
                data = _dbSAGE.getData(sql)

                If Not IsNothing(data) Then
                    sql = "UPDATE [IT_CABECERAPARTES] SET " &
                              "IT_HORASND = " & data.Rows(0)("TotalHorasND").ToString.Replace(",", ".") & ", " &
                              "IT_HORASEXTRA = " & data.Rows(0)("TotalHorasExtra").ToString.Replace(",", ".") & ", " &
                              "IT_TOTALHORAS = " & data.Rows(0)("TotalHoras").ToString.Replace(",", ".") & ", " &
                              "IT_HORASDESPLAZAMIENTO = " & data.Rows(0)("TotalHorasDesplazamiento").ToString.Replace(",", ".") & ", " &
                              "IT_KILOMETROS = " & data.Rows(0)("TotalKilometros").ToString.Replace(",", ".") & ", " &
                              "IT_DIETACOMPLETA = " & data.Rows(0)("TotalDietaCompleta").ToString.Replace(",", ".") & ", " &
                              "IT_MEDIADIETA = " & data.Rows(0)("TotalMediaDieta").ToString.Replace(",", ".") & ", " &
                              "IT_GASTOSGENERALES = " & data.Rows(0)("TotalGastosGenerales").ToString.Replace(",", ".") & " " &
                          "WHERE CodigoEmpresa='" & item.Split(";")(0) & "' AND Fecha='" & item.Split(";")(1) & "' AND Operario='" & item.Split(";")(2) & "'"
                    _dbSAGE.query(sql)
                End If
                _dbSAGE.closeDatatable(data)
            Next


            Try
                '
                '   Notifico que he terminado de procesar los partes de cada supervisor
                '
                Dim whoOKStr As String = "-99"
                For Each who As Integer In whoOK
                    whoOKStr &= "," & who
                Next

                json = New StringContent("{ ""who"": """ & whoOKStr & """}", System.Text.Encoding.UTF8, "application/json")
                response = client.PostAsync(_urlSync & "download-workpartEND", json).Result
                If response.StatusCode <> 200 Then Exit Try
                payload = response.Content.ReadAsStringAsync().Result
                responseJSON = Newtonsoft.Json.Linq.JObject.Parse(payload)
                If Not Convert.ToBoolean(responseJSON.Item("isOK")) Then Exit Try
            Catch ex As Exception
                eleantec.Library.Utilities.Write2EventLog("errProcesando workpartEND: " & ex.Message, TraceEventType.Critical)
            End Try
        Catch ex As Exception
            eleantec.Library.Utilities.Write2EventLog("Error al procesar la descarga de los partes de trabajo: " & ex.Message, TraceEventType.Error)
        End Try




        If nPartesTot > 0 Then eleantec.Library.Utilities.Write2EventLog("Procesamiento terminado: " & nPartes & " partes generados de un total " & nPartesTot & " partes", TraceEventType.Information)
        Return True
    End Function

#End Region


#Region "Varios"

    Public Sub sayEndAll2server()
        Dim client As HttpClient = Nothing
        Dim response As HttpResponseMessage = Nothing
        Dim payload As String = ""

        Dim json As StringContent = Nothing
        Dim responseJSON As Newtonsoft.Json.Linq.JObject = Nothing

        Try
            client = New HttpClient() : client.Timeout = New TimeSpan(TimeSpan.TicksPerSecond * 5) : client.BaseAddress = New Uri(_urlSync) : client.DefaultRequestHeaders.Add("Accept", "application/json")
            client.DefaultRequestHeaders.Add("Token", _appConfig.token)

            json = New StringContent("{ ""code"": ""52""}", System.Text.Encoding.UTF8, "application/json")
            response = client.PostAsync(_urlSync & "task-sayEND", json).Result
            If response.StatusCode <> 200 Then Exit Try
            payload = response.Content.ReadAsStringAsync().Result
            responseJSON = Newtonsoft.Json.Linq.JObject.Parse(payload)
            If Not Convert.ToBoolean(responseJSON.Item("isOK")) Then
                eleantec.Library.Utilities.Write2EventLog("Error al notificar el fin del proceso: " & responseJSON.Item("message").ToString, TraceEventType.Error)
                Return
            End If
        Catch ex As Exception
        End Try

    End Sub


    ''' <summary>
    ''' Comprobación de ordenes de trabajo que no estan descargadadas de la aplicación
    ''' </summary>
    ''' <returns></returns>
    Private Function reportPrint() As Boolean
        Dim sql As String = ""
        Dim nOrders As Integer = 0, id As Integer = 0

        Dim json As StringContent = Nothing

        Dim client As HttpClient = Nothing
        Dim response As HttpResponseMessage = Nothing
        Dim payload As String = ""

        Dim responseJSON As Newtonsoft.Json.Linq.JObject = Nothing, responseJSONOK As Newtonsoft.Json.Linq.JObject = Nothing
        Dim responseJSONOrders As Newtonsoft.Json.Linq.JArray


        client = New HttpClient() : client.Timeout = New TimeSpan(TimeSpan.TicksPerSecond * 5) : client.BaseAddress = New Uri(_urlSync) : client.DefaultRequestHeaders.Add("Accept", "application/json")
        client.DefaultRequestHeaders.Add("Token", _appConfig.token)


        '
        '   Preparo la impresión del informe de Crystal Reports comprobando que el ODBC de conexión a la base de datos local esta creado
        '
        If Not eleantec.Library.Utilities.createODBC(My.Application.Info.ProductName, _dbLocal.getConnectionString) Then
            eleantec.Library.Utilities.Write2EventLog("Error al crear el ODBC de conexión a la base de datos local", TraceEventType.Error)
            Return False
        End If



        Try
            '
            '   Solicito el listado de PEDIDOS procesables (Si no hay nada que procesar me da un isOK false y no hago nada)
            '
            json = New StringContent("{ ""code"": """ & "52" & """}", System.Text.Encoding.UTF8, "application/json")
            response = client.PostAsync(_urlSync & "download-orders", json).Result
            If response.StatusCode <> 200 Then Exit Try
            payload = response.Content.ReadAsStringAsync().Result
            responseJSON = Newtonsoft.Json.Linq.JObject.Parse(payload)
            If Not Convert.ToBoolean(responseJSON.Item("isOK")) Then Exit Try

            'Console.WriteLine(responseJSON.Item("response").Item("orders"))
            responseJSONOrders = responseJSON.Item("response").Item("orders")


            '
            '   Proceso los pedidos descargados
            '
            For Each order As Newtonsoft.Json.Linq.JObject In responseJSONOrders
                '
                '   Agrego la cabecera del pedido
                '
                nOrders += 1
                Console.WriteLine("workorder: " & order.Item("workorder").ToString)
                sql = "INSERT INTO tmporders (customerName, customerLocation, date, about, worker, workorder, ref, type, hasPhotos, observations) VALUES (" &
                        "'" & order.Item("customerName").ToString & "'," &
                        "'" & order.Item("customerLocation").ToString & "'," &
                        "'" & order.Item("date").ToString & "'," &
                        "'" & order.Item("about").ToString & "'," &
                        "'" & order.Item("workerCode").ToString & " - " & order.Item("worker").ToString & "'," &
                        "'" & order.Item("workorder").ToString & "'," &
                        "'" & order.Item("ref").ToString & "'," &
                        "'" & order.Item("type").ToString & "'," &
                        "'" & order.Item("hasPhotos").ToString & "'," &
                        "'" & order.Item("observations").ToString & "'" &
                    ")"
                _dbLocal.query(sql)
                id = _dbLocal.getLastInsertID


                '
                '   Proceso las lineas
                '
                For Each line As Newtonsoft.Json.Linq.JObject In order.Item("lines")
                    sql = "INSERT INTO tmporders_lines (id_order, description, units) VALUES (" &
                            "'" & id & "'," &
                            "'" & line.Item("description").ToString & "'," &
                            "'" & line.Item("units").ToString & "'" &
                        ")"
                    _dbLocal.query(sql)
                Next

                System.Threading.Thread.Sleep(450)

                '
                '   Procedo a la impresión
                '
                Dim rpt As New rptOrderApp
                eleantec.Library.Utilities.logonInMySQLServer(rpt, _databaseCfgLocal)
                rpt.DataDefinition.FormulaFields("Tipo").Text = "'" & "PEDIDO" & "'"
                rpt.DataDefinition.FormulaFields("Uds").Text = "'" & "UD SERVIDAS" & "'"
                rpt.RecordSelectionFormula = "{_tmporders.id} = " & id
                rpt.PrintOptions.PrinterName = _appConfig.printerOrders
                rpt.PrintOptions.PaperSource = CrystalDecisions.Shared.PaperSource.Auto
                rpt.PrintToPrinter(1, True, 1, 1)
                rpt.Dispose() : rpt = Nothing

                Try
                    json = New StringContent("{ ""orderNo"": """ & order.Item("orderNo").ToString & """}", System.Text.Encoding.UTF8, "application/json")
                    response = client.PostAsync(_urlSync & "download-orderOK", json).Result
                    If response.StatusCode <> 200 Then Exit Try
                    payload = response.Content.ReadAsStringAsync().Result
                    responseJSON = Newtonsoft.Json.Linq.JObject.Parse(payload)
                    If Not Convert.ToBoolean(responseJSON.Item("isOK")) Then Exit For
                Catch ex As Exception

                End Try

                System.Threading.Thread.Sleep(350)
            Next

        Catch ex As Exception
            eleantec.Library.Utilities.Write2EventLog("Error al descargar los pedidos >> " & ex.Message, TraceEventType.Error)
        End Try



        Try
            '
            '   Solicito el listado de ABONOS procesable (Si no hay nada que procesar me da un isOK false y no hago nada)
            '
            json = New StringContent("{ ""code"": """ & "52" & """}", System.Text.Encoding.UTF8, "application/json")
            response = client.PostAsync(_urlSync & "download-salesNotes", json).Result
            If response.StatusCode <> 200 Then Exit Try
            payload = response.Content.ReadAsStringAsync().Result
            responseJSON = Newtonsoft.Json.Linq.JObject.Parse(payload)
            If Not Convert.ToBoolean(responseJSON.Item("isOK")) Then Exit Try

            Console.WriteLine(responseJSON.Item("response").Item("salesNotes"))
            responseJSONOrders = responseJSON.Item("response").Item("salesNotes")



            '
            '   Proceso los pedidos descargados
            '
            For Each salesNote As Newtonsoft.Json.Linq.JObject In responseJSONOrders
                '
                '   Agrego la cabecera del pedido
                '
                nOrders += 1
                Console.WriteLine("workorder: " & salesNote.Item("workorder").ToString)
                sql = "INSERT INTO tmporders (id, customerName, customerLocation, date, about, worker, workorder, ref, type, observations) VALUES (" &
                        "'" & salesNote.Item("customerName").ToString & "'," &
                        "'" & salesNote.Item("customerLocation").ToString & "'," &
                        "'" & salesNote.Item("date").ToString & "'," &
                        "'" & salesNote.Item("about").ToString & "'," &
                        "'" & salesNote.Item("workerCode").ToString & " - " & salesNote.Item("worker").ToString & "'," &
                        "'" & salesNote.Item("workorder").ToString & "'," &
                        "'" & salesNote.Item("ref").ToString & "'," &
                        "'" & salesNote.Item("type").ToString & "'," &
                        "'" & salesNote.Item("observations").ToString & "'" &
                    ")"
                _dbLocal.query(sql)
                id = _dbLocal.getLastInsertID

                '
                '   Proceso las lineas
                '
                For Each line As Newtonsoft.Json.Linq.JObject In salesNote.Item("lines")
                    sql = "INSERT INTO tmporders_lines (id_order, description, units, ref) VALUES (" &
                            "'" & id & "'," &
                            "'" & line.Item("description").ToString & "'," &
                            "'" & line.Item("units").ToString & "'," &
                            "'" & line.Item("ref").ToString & "'" &
                        ")"
                    _dbLocal.query(sql)
                Next

                System.Threading.Thread.Sleep(450)


                '
                '   Procedo a la impresión
                '
                Dim rpt As New rptOrderApp
                eleantec.Library.Utilities.logonInMySQLServer(rpt, _databaseCfgLocal)
                rpt.DataDefinition.FormulaFields("Tipo").Text = "'" & "ABONO" & "'"
                rpt.DataDefinition.FormulaFields("Uds").Text = "'" & "UD ABONADAS" & "'"
                rpt.RecordSelectionFormula = "{_tmporders.id} = " & id
                rpt.PrintOptions.PrinterName = _appConfig.printerOrders
                rpt.PrintOptions.PaperSource = CrystalDecisions.Shared.PaperSource.Auto
                rpt.PrintToPrinter(1, True, 1, 1)
                rpt.Dispose() : rpt = Nothing

                Try
                    json = New StringContent("{ ""salesNoteNo"": """ & salesNote.Item("salesNoteNo").ToString & """}", System.Text.Encoding.UTF8, "application/json")
                    response = client.PostAsync(_urlSync & "download-salesNoteOK", json).Result
                    If response.StatusCode <> 200 Then Exit Try
                    payload = response.Content.ReadAsStringAsync().Result
                    responseJSON = Newtonsoft.Json.Linq.JObject.Parse(payload)
                    If Not Convert.ToBoolean(responseJSON.Item("isOK")) Then Exit For
                Catch ex As Exception

                End Try
            Next

        Catch ex As Exception
            eleantec.Library.Utilities.Write2EventLog("Error al descargar los abonos >> " & ex.Message, TraceEventType.Error)
        End Try

        Return True
    End Function

#End Region


#Region "Funciones adicionales"
    Private Function getValue(ByVal value As Object) As String
        If value Is Nothing Then Return ""
        If value Is DBNull.Value Then Return ""
        If TypeOf (value) Is DateTime Then Return Convert.ToDateTime(value).ToString("yyyy-MM-dd HH:mm:ss").Replace(" 00:00:00", "")
        If TypeOf (value) Is Decimal Then Return Convert.ToDouble(value).ToString("0.00").Replace(",", ".")

        Return value.ToString.Replace("'", "").Replace("""", "´").Replace(vbCrLf, "\n")
    End Function

    Private Function getUserPassword(ByVal value As String) As String
        Dim pass As String = ""
        For i As Integer = 0 To value.Length - 1
            If IsNumeric(value.Substring(i, 1)) Then
                If pass.Length = 0 And value.Substring(i, 1) = "0" Then Continue For
                pass &= value.Substring(i, 1)
                If pass.Length >= 4 Then Exit For
            End If
        Next
        If pass.Length < 4 Then pass = pass.PadLeft(4, "1")
        Return pass
    End Function

    Public Function AcceptAllCertifications(ByVal sender As Object, ByVal certification As System.Security.Cryptography.X509Certificates.X509Certificate, ByVal chain As System.Security.Cryptography.X509Certificates.X509Chain, ByVal sslPolicyErrors As System.Net.Security.SslPolicyErrors) As Boolean
        Return True
    End Function

#End Region

End Module
