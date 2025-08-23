Imports System.Collections.Concurrent
Imports System.Runtime.InteropServices
Imports MaterialSkin
Imports Microsoft.Web.WebView2.Core
Imports Microsoft.Web.WebView2.Core.DevToolsProtocolExtension
Imports Microsoft.Web.WebView2.WinForms
Imports PowerballGame.cFareMember
Imports PowerballGame.cFarePattern_Text
Imports PowerballGame.cGameState
Imports PowerballGame.frmMain

Public Class frmMain
    ' 웹소켓 정보를 저장할 컬렉션
    Private webSockets As New Dictionary(Of String, WebSocketInfo)
    ' 새 창 WebView2 컨트롤을 관리하기 위한 컬렉션
    Private newWindows As New Dictionary(Of String, NewWindowInfo)
    ' 프레임 정보를 저장하기 위한 컬렉션
    Private frames As New Dictionary(Of String, FrameInfo)
    Private _SyncLock As New Object()
    Private _GameList As New List(Of HS_History)
    Private _cur_game As New cJsonGet.WS_Multi_powerball_player_gameState.Rootobject
    Private _mesu As Integer = 6
    Public mesu_list_all As New List(Of mesu_class)
    Public cur_member As HS_Member
    Public _BetList As New List(Of _Bet)
    Public _SumList As New List(Of _Sum)
    Private _cMultiPattern As cFareMultiPattern
    Private _cMultiPattern2 As cFareMultiPattern2
    Dim _all_cFarePattern As cFarePattern
    Dim mesu_list_run As New List(Of mesu_class)
    Public _lastBet As DateTime
    Dim bStart As Boolean = False
    Dim zzzz As Boolean = False

    ' Virtual ListView Data Collections
    Private lst패턴설정Data As New List(Of String())
    Private lst단계별통계Data As New List(Of String())
    Private ReadOnly dataLock As New Object()

#Region "[_Bet Class List]"
    Public Class _Bet
        Public Property _Amount As Integer
        Public Property _ResultAmount As Integer = 0
        Public Property _BetVal As String
        Public Property _ResultVal As String = ""
        Public Property _BetType As Integer
        Public Property _GameType As String = ""
        Public Property _GameType2 As String = ""
        Public Property _Mesu As Integer = 0
        Public Property _RowCnt As Integer = 0
        Public Property _Level As String = ""

        Sub New(__Amount As Integer, __BetVal As String, __BetType As Integer, __GameType As String, __GameType2 As String, __Mesu As Integer, __RowCnt As Integer, __Level As String)
            _Amount = __Amount
            _BetVal = __BetVal
            _BetType = __BetType
            _GameType = __GameType
            _GameType2 = __GameType2
            _Mesu = __Mesu
            _RowCnt = __RowCnt
            _Level = __Level
        End Sub
    End Class

    Public Class _Sum
        Public Property _Mesu As Integer
        Public Property _BetType As Integer
        Public Property _GameType As String
        Public Property _GameType2 As String
        Public Property _RowCnt As Integer
        Sub New(__Mesu As Integer, __BetType As Integer, __GameType As String, __GameType2 As String)
            _Mesu = __Mesu
            _BetType = __BetType
            _GameType = __GameType
            _GameType2 = __GameType2
        End Sub
    End Class
#End Region
#Region "[Property List]"
    Private ReadOnly Property p_metrics_verify(replyToId As String, result As String) As String
        Get
            Dim _Rootobject As New cJsonSet.WS_Multi_metrics_verify.Rootobject
            Dim _Args As New cJsonSet.WS_Multi_metrics_verify.Args

            _Args.replyToId = replyToId
            _Args.result = result
            _Rootobject.args = _Args

            Return Newtonsoft.Json.JsonConvert.SerializeObject(_Rootobject)
        End Get

    End Property
    Private ReadOnly Property p_CLIENT_BALANCE_UPDATED(balance As Integer, updateBalanceMessageId As String) As String
        Get
            Dim _Rootobject As New cJsonSet.WS_Multi_CLIENT_BALANCE_UPDATED.Rootobject
            Dim _Log As New cJsonSet.WS_Multi_CLIENT_BALANCE_UPDATED.Log
            Dim _Value As New cJsonSet.WS_Multi_CLIENT_BALANCE_UPDATED.Value
            Dim _Gamedimensions As New cJsonSet.WS_Multi_CLIENT_BALANCE_UPDATED.Gamedimensions

            _Value.gameDimensions = _Gamedimensions
            _Value.balance = balance
            _Value.updateBalanceMessageId = updateBalanceMessageId
            _Log.value = _Value
            _Rootobject.log = _Log

            Return Newtonsoft.Json.JsonConvert.SerializeObject(_Rootobject)
        End Get

    End Property
    Private ReadOnly Property p_fetchBalance As String
        Get
            Dim _Rootobject As New cJsonSet.WS_Multi_fetchBalance.Rootobject
            Dim _Args As New cJsonSet.WS_Multi_fetchBalance.Args
            _Rootobject.args = _Args

            Return Newtonsoft.Json.JsonConvert.SerializeObject(_Rootobject)
        End Get
    End Property
    Private ReadOnly Property p_settings_read As String
        Get
            Dim _Rootobject As New cJsonSet.WS_Multi_setting_read.Rootobject
            Dim _Args As New cJsonSet.WS_Multi_setting_read.Args
            _Args.keys.Add("generic.common")
            _Args.keys.Add("generic.mobile")
            _Args.keys.Add("generic.phone")
            _Args.keys.Add("generic.tablet")
            _Args.keys.Add("powerball.common")
            _Rootobject.args = _Args

            Return Newtonsoft.Json.JsonConvert.SerializeObject(_Rootobject)
        End Get
    End Property
    Private ReadOnly Property p_metrics_ping() As String
        Get
            Dim _Rootobject As New cJsonSet.WS_Multi_metrics_ping.Rootobject
            Dim _Args As New cJsonSet.WS_Multi_metrics_ping.Args

            _Rootobject.args = _Args

            Return Newtonsoft.Json.JsonConvert.SerializeObject(_Rootobject)
        End Get
    End Property

    Private ReadOnly Property p_WS_History(bBool As Boolean) As String
        Get
            Dim _Rootobject As New cJsonSet.WS_History.Rootobject
            Dim _Log As New cJsonSet.WS_History.Log
            Dim _Gamedimensions As New cJsonSet.WS_History.Gamedimensions
            Dim _Value As New cJsonSet.WS_History.Value

            Dim _message As String = ""
            If bBool Then
                _message = "true"
            Else
                _message = "false"
            End If
            _Value.message = _message
            _Value.gameId = _cur_game.args.gameId
            _Value.gameDimensions = _Gamedimensions
            _Log.value = _Value
            _Rootobject.log = _Log

            Return Newtonsoft.Json.JsonConvert.SerializeObject(_Rootobject)
        End Get
    End Property


    Private ReadOnly Property p_changeBetAmount(bet As _Bet, _gameid As String, bettype As String) As String
        Get
            Dim _Rootobject As New cJsonSet.WS_ChangeAmount.Rootobject
            Dim _Args As New cJsonSet.WS_ChangeAmount.Args
            Dim _Bettags As New cJsonSet.WS_ChangeAmount.Bettags

            Dim _betSpot As String = ""
            If bettype = "1" Then
                Select Case bet._BetType
                    Case 0
                        If bet._GameType = "일반볼" Then
                            Select Case bet._BetVal
                                Case "언더"
                                    _betSpot = "RegularUnder"
                                Case "오버"
                                    _betSpot = "RegularOver"
                                Case "홀"
                                    _betSpot = "RegularOdd"
                                Case "짝"
                                    _betSpot = "RegularEven"
                            End Select
                        ElseIf bet._GameType = "파워볼" Then
                            Select Case bet._BetVal
                                Case "언더"
                                    _betSpot = "PowerUnder"
                                Case "오버"
                                    _betSpot = "PowerOver"
                                Case "홀"
                                    _betSpot = "PowerOdd"
                                Case "짝"
                                    _betSpot = "PowerEven"
                            End Select
                        End If
                    Case 1
                End Select
            Else
                If bet._BetVal = "대" Then
                    _betSpot = "RegularOddLarge"
                ElseIf bet._BetVal = "중" Then
                    _betSpot = "RegularOddMedium"
                Else
                    _betSpot = "RegularOddSmall"
                End If
            End If

            _Args.gameId = _gameid
            _Args.betSpot = _betSpot
            _Args.amount = bet._Amount
            _Args.betTags = _Bettags
            _Rootobject.args = _Args
            Return Newtonsoft.Json.JsonConvert.SerializeObject(_Rootobject)
        End Get
    End Property

    '
    Private ReadOnly Property p_Bet(bet As _Bet, betlist As List(Of _Bet), gametime As String, gameid As String) As String
        Get
            Dim _Rootobject As New cJsonSet.WS_Bet.Rootobject
            Dim _Bets As New Dictionary(Of String, Integer)
            Dim _Betsingame As New Dictionary(Of String, Integer)
            Dim _Codes As New Dictionary(Of String, Integer)
            Dim _Gamedimensions As New cJsonSet.WS_Bet.Gamedimensions
            Dim _Log As New cJsonSet.WS_Bet.Log
            Dim _Value As New cJsonSet.WS_Bet.Value

            Select Case bet._BetType
                Case 0
                    If bet._GameType = "일반볼" Then
                        Select Case bet._BetVal
                            Case "언더"
                                _Bets.Add("RegularUnder", bet._Amount)
                                _Codes.Add("PB_RegularUnder", bet._Amount)
                            Case "오버"
                                _Bets.Add("RegularOver", bet._Amount)
                                _Codes.Add("PB_RegularOver", bet._Amount)
                            Case "홀"
                                _Bets.Add("RegularOdd", bet._Amount)
                                _Codes.Add("PB_RegularOdd", bet._Amount)
                            Case "짝"
                                _Bets.Add("RegularEven", bet._Amount)
                                _Codes.Add("PB_RegularEven", bet._Amount)
                        End Select
                    ElseIf bet._GameType = "파워볼" Then
                        Select Case bet._BetVal
                            Case "언더"
                                _Bets.Add("PowerUnder", bet._Amount)
                                _Codes.Add("PB_PowerUnder", bet._Amount)
                            Case "오버"
                                _Bets.Add("PowerOver", bet._Amount)
                                _Codes.Add("PB_PowerOver", bet._Amount)
                            Case "홀"
                                _Bets.Add("PowerOdd", bet._Amount)
                                _Codes.Add("PB_PowerOdd", bet._Amount)
                            Case "짝"
                                _Bets.Add("PowerEven", bet._Amount)
                                _Codes.Add("PB_PowerEven", bet._Amount)
                        End Select
                    End If
                Case 1
            End Select

            _BetList.ForEach(Sub(x)
                                 Select Case bet._BetType
                                     Case 0
                                         If bet._GameType = "일반볼" Then
                                             Select Case bet._BetVal
                                                 Case "언더"
                                                     _Betsingame.Add("RegularUnder", bet._Amount)
                                                 Case "오버"
                                                     _Betsingame.Add("RegularOver", bet._Amount)
                                                 Case "홀"
                                                     _Betsingame.Add("RegularOdd", bet._Amount)
                                                 Case "짝"
                                                     _Betsingame.Add("RegularEven", bet._Amount)
                                             End Select
                                         ElseIf bet._GameType = "파워볼" Then
                                             Select Case bet._BetVal
                                                 Case "언더"
                                                     _Betsingame.Add("PowerUnder", bet._Amount)
                                                 Case "오버"
                                                     _Betsingame.Add("PowerOver", bet._Amount)
                                                 Case "홀"
                                                     _Betsingame.Add("PowerOdd", bet._Amount)
                                                 Case "짝"
                                                     _Betsingame.Add("PowerEven", bet._Amount)
                                             End Select
                                         End If
                                     Case 1
                                 End Select
                             End Sub)

            _Value.betsInGame = _Betsingame
            _Value.codes = _Codes
            _Value.bets = _Bets
            _Value.gameTime = gametime
            _Value.gameId = gameid
            _Value.amount = bet._Amount
            _Value.chipstack.Add(2000)
            _Value.chipstack.Add(10000)
            _Value.chipstack.Add(50000)
            _Value.chipstack.Add(200000)
            _Value.chipstack.Add(1000000)
            _Value.chipstack.Add(2000000)
            _Value.gameDimensions = _Gamedimensions
            _Log.value = _Value
            _Rootobject.log = _Log

            Return Newtonsoft.Json.JsonConvert.SerializeObject(_Rootobject)
        End Get
    End Property
    Private ReadOnly Property p_Bet2(bet As _Bet, betlist As List(Of _Bet), gametime As String, gameid As String) As String
        Get
            Dim _Rootobject As New cJsonSet.WS_Bet.Rootobject
            Dim _Bets As New Dictionary(Of String, Integer)
            Dim _Betsingame As New Dictionary(Of String, Integer)
            Dim _Codes As New Dictionary(Of String, Integer)
            Dim _Gamedimensions As New cJsonSet.WS_Bet.Gamedimensions
            Dim _Log As New cJsonSet.WS_Bet.Log
            Dim _Value As New cJsonSet.WS_Bet.Value
            '
            If bet._BetVal = "대" Then
                _Bets.Add("RegularOddLarge", bet._Amount)
                _Codes.Add("PB_RegularOddLarge", bet._Amount)
            ElseIf bet._BetVal = "중" Then
                _Bets.Add("RegularOddMedium", bet._Amount)
                _Codes.Add("PB_RegularOddMedium", bet._Amount)
            Else
                _Bets.Add("RegularOddSmall", bet._Amount)
                _Codes.Add("PB_RegularOddSmall", bet._Amount)
            End If

            _BetList.ForEach(Sub(x)
                                 Select Case bet._BetVal
                                     Case "대"
                                         _Betsingame.Add("RegularOddLarge", bet._Amount)
                                     Case "중"
                                         _Betsingame.Add("RegularOddMedium", bet._Amount)
                                     Case "소"
                                         _Betsingame.Add("RegularOddSmall", bet._Amount)
                                 End Select
                             End Sub)

            _Value.betsInGame = _Betsingame
            _Value.codes = _Codes
            _Value.bets = _Bets
            _Value.gameTime = gametime
            _Value.gameId = gameid
            _Value.chipstack.Add(1000)
            _Value.chipstack.Add(5000)
            _Value.chipstack.Add(25000)
            _Value.chipstack.Add(100000)
            _Value.chipstack.Add(500000)
            _Value.chipstack.Add(1000000)
            _Value.gameDimensions = _Gamedimensions
            _Log.value = _Value
            _Rootobject.log = _Log

            Return Newtonsoft.Json.JsonConvert.SerializeObject(_Rootobject)
        End Get
    End Property
#End Region

    Private Async Sub frmMain_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        Control.CheckForIllegalCrossThreadCalls = False
        Dim SkinManager As MaterialSkinManager = MaterialSkinManager.Instance
        SkinManager.AddFormToManage(Me)
        SkinManager.Theme = MaterialSkinManager.Themes.LIGHT
        SkinManager.ColorScheme = New ColorScheme(Primary.Blue800, Primary.Blue900, Primary.Blue500, Accent.Blue200, TextShade.WHITE)

        Dim cFareMember As cFareMember = New cFareMember()

        'cur_member = cFareMember.memberList.First

        txtUrlBox.Text = cur_member.member_site

        ' WebView2 초기화
        Await InitializeWebView2Async()

        For ii = 2 To 10
            Dim iiii As Integer = ii
            Dim mesu_list_c As mesu_class
            Dim mesu_list As New List(Of Integer)
            Dim jj As Integer = 1
            Dim iCnt As Integer = 0

            For i = 0 To iiii - 1
                mesu_list = New List(Of Integer)
                Dim iVal As Integer = i + 1
                mesu_list.Add(iVal)

                Do While iVal < 10000
                    iVal += iiii
                    mesu_list.Add(iVal)
                Loop
                mesu_list_c = New mesu_class(ii, iCnt, mesu_list) ' New mesu_class(ii, mesu_list_all.Count, mesu_list)
                iCnt += 1
                mesu_list_all.Add(mesu_list_c)
            Next
        Next

        ComboBox1.Text = 6

#Region "[lst패턴설정 설정]"
        ''Set view property
        lst패턴설정.View = View.Details
        lst패턴설정.GridLines = True
        lst패턴설정.FullRowSelect = True
        lst패턴설정.VirtualMode = True

        lst패턴설정.Columns.Clear()

        'Add column header
        lst패턴설정.Columns.Add("배팅타입", 0)
        lst패턴설정.Columns.Add("패턴", 100, HorizontalAlignment.Center)
        lst패턴설정.Columns.Add("매수", 40, HorizontalAlignment.Center)
        lst패턴설정.Columns.Add("단계", 50, HorizontalAlignment.Center)
        lst패턴설정.Columns.Add("승/무/패", 60, HorizontalAlignment.Center)
        lst패턴설정.Columns.Add("픽", 40, HorizontalAlignment.Center)
        lst패턴설정.Columns.Add("결과", 0)
        lst패턴설정.Columns.Add("배팅금액", 80, HorizontalAlignment.Center)
        lst패턴설정.Columns.Add("gametype", 0)
        lst패턴설정.Columns.Add("gametype2", 0)
        lst패턴설정.Columns.Add("패턴reckey", 0)

        ' Initialize virtual data
        SyncLock dataLock
            lst패턴설정Data.Clear()
        End SyncLock
        lst패턴설정.VirtualListSize = 0

#End Region
#Region "[lst단계별통계 설정]"
        ''Set view property
        lst단계별통계.View = View.Details
        lst단계별통계.GridLines = True
        lst단계별통계.FullRowSelect = True
        lst단계별통계.VirtualMode = True

        'Add column header
        lst단계별통계.Columns.Add("매수", 0, HorizontalAlignment.Center)
        lst단계별통계.Columns.Add("매수", 40, HorizontalAlignment.Center)
        lst단계별통계.Columns.Add("볼타입", 50, HorizontalAlignment.Center)
        lst단계별통계.Columns.Add("게임타입", 60, HorizontalAlignment.Center)
        lst단계별통계.Columns.Add("대중소", 50, HorizontalAlignment.Center)
        lst단계별통계.Columns.Add("최고단계", 60, HorizontalAlignment.Center)
        lst단계별통계.Columns.Add("최고배팅액", 100, HorizontalAlignment.Center)

        ' Initialize virtual data
        SyncLock dataLock
            lst단계별통계Data.Clear()
        End SyncLock
        lst단계별통계.VirtualListSize = 0

#End Region
#Region "[lst멀티패턴단일설정 설정]"

        ''Set view property
        lst멀티패턴단일설정.View = View.Details
        lst멀티패턴단일설정.GridLines = True
        lst멀티패턴단일설정.FullRowSelect = True

        'Add column header
        lst멀티패턴단일설정.Columns.Add("Key", 0)
        lst멀티패턴단일설정.Columns.Add("패턴타입", 0)
        lst멀티패턴단일설정.Columns.Add("매수", 50)
        lst멀티패턴단일설정.Columns.Add("볼타입", 50)
        lst멀티패턴단일설정.Columns.Add("게임타입", 70)
        lst멀티패턴단일설정.Columns.Add("패턴명", 150)
        lst멀티패턴단일설정.Columns.Add("패턴키", 0)
#End Region
#Region "[lst멀티패턴대중소설정 설정]"

        ''Set view property
        lst멀티패턴대중소설정.View = View.Details
        lst멀티패턴대중소설정.GridLines = True
        lst멀티패턴대중소설정.FullRowSelect = True

        'Add column header
        lst멀티패턴대중소설정.Columns.Add("Key", 0)
        lst멀티패턴대중소설정.Columns.Add("패턴타입", 0)
        lst멀티패턴대중소설정.Columns.Add("매수", 50)
        lst멀티패턴대중소설정.Columns.Add("대중소", 50)
        lst멀티패턴대중소설정.Columns.Add("역방향", 70)
        lst멀티패턴대중소설정.Columns.Add("패턴명", 150)
        lst멀티패턴대중소설정.Columns.Add("패턴키", 0)
#End Region

        RefreshPatternCombo()
        cboBetType.SelectedIndex = 0
        RefreshMultiPattern2Combo()

    End Sub

#Region "[웹뷰 이벤트]"

    Private Async Function InitializeWebView2Async() As Task
        Try
            ' WebView2 환경 초기화
            Await WebView21.EnsureCoreWebView2Async(Nothing)

            ' 새 창 이벤트 처리
            AddHandler WebView21.CoreWebView2.NewWindowRequested, AddressOf CoreWebView2_NewWindowRequested

            ' 프레임 탐색 이벤트 처리
            AddHandler WebView21.CoreWebView2.FrameNavigationStarting, AddressOf CoreWebView2_FrameNavigationStarting
            AddHandler WebView21.CoreWebView2.FrameNavigationCompleted, AddressOf CoreWebView2_FrameNavigationCompleted
            AddHandler WebView21.CoreWebView2.FrameCreated, AddressOf CoreWebView2_FrameCreated

            ' !!! 중요: NavigationCompleted 이벤트 핸들러 추가 !!!
            AddHandler WebView21.CoreWebView2.NavigationCompleted, AddressOf WebView21_NavigationCompleted

            ' CDP를 통한 웹소켓 이벤트 구독
            Await SubscribeToWebSocketEventsAsync(WebView21.CoreWebView2)
            '초기 URL로 이동
            WebView21.CoreWebView2.Navigate(txtUrlBox.Text)

        Catch ex As Exception
            MessageBox.Show($"WebView2 초기화 오류: {ex.Message}", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Function

    ' 페이지 이동 완료 후 후킹 주입
    Private Async Sub WebView21_NavigationCompleted(sender As Object, e As CoreWebView2NavigationCompletedEventArgs)
        Await InjectWebSocketHookAsync()
    End Sub

    ' WebSocket 후킹 스크립트 (전송 + 수신 + 연결 로그 포함, 중복 방지)
    Private ReadOnly websocketHookScript As String = "
        (function hookWebSocketInAllFrames() {
        if (window.__wsHooked) return;
        window.__wsHooked = true;

        const OriginalWebSocket = window.WebSocket;
        window.__wsList = [];

        function HookedWebSocket(url, protocols) {
            const ws = protocols ? new OriginalWebSocket(url, protocols) : new OriginalWebSocket(url);
            window.__wsList.push(ws);
            return ws;
        }

        HookedWebSocket.OPEN = OriginalWebSocket.OPEN;
        HookedWebSocket.CONNECTING = OriginalWebSocket.CONNECTING;
        HookedWebSocket.CLOSING = OriginalWebSocket.CLOSING;
        HookedWebSocket.CLOSED = OriginalWebSocket.CLOSED;
        HookedWebSocket.prototype = OriginalWebSocket.prototype;

        // 현재 창 후킹
        window.WebSocket = HookedWebSocket;
        console.log('[WebSocket Hook] 현재 window 후킹 완료');

        // iframe 후킹 (Same-Origin만)
        const frames = document.querySelectorAll(""iframe"");
        for (const frame of frames) {
            try {
                frame.contentWindow.WebSocket = HookedWebSocket;
                console.log('[WebSocket Hook] iframe 후킹 성공:', frame.src);
            } catch (e) {
                console.warn('[WebSocket Hook] iframe 접근 불가:', frame.src);
            }
        }
    })();
    "

    ' WebSocket 후킹 스크립트 (전송 + 수신 + 연결 로그 포함, 중복 방지)
    Private ReadOnly iframeHookScript As String = "
                (function hookIframes() {
            window.__iframeList = [];

            const iframes = document.querySelectorAll(""iframe"");
            for (const frame of iframes) {
                try {
                    const src = frame.src || frame.contentWindow.location.href;
                    window.__iframeList.push({
                        src: src,
                        frame: frame,
                        win: frame.contentWindow
                    });
                    console.log('[iframe hook] 저장됨:', src);
                } catch (e) {
                    console.warn('[iframe hook] 접근 불가:', frame);
                }
            }
        })();
    "

    ' 후킹 주입 함수
    Public Async Function InjectWebSocketHookAsync() As Task
        Await WebView21.CoreWebView2.ExecuteScriptAsync(websocketHookScript)
        Await WebView21.CoreWebView2.ExecuteScriptAsync(iframeHookScript)
    End Function

    Public Async Function SendClickAsync(partialUrl As String, message As String) As Task(Of String)
        Dim scriptTemplate As String = "
            (function clickHistoryButton() {
                const btn = document.querySelector('[data-role=""history-button""]');
                if (!btn) {
                    console.warn('❌ 버튼을 찾을 수 없습니다');
                    return;
                }

                // 클릭 대상이 되는 요소(버튼 영역 내부 가장 큰 부분 클릭)
                const rect = btn.getBoundingClientRect();
                const x = rect.left + rect.width / 2;
                const y = rect.top + rect.height / 2;

                const eventOpts = {
                    bubbles: true,
                    cancelable: true,
                    view: window,
                    clientX: x,
                    clientY: y
                };

                // 마우스 이벤트 순서대로 발생시킴
                ['mouseover', 'mousedown', 'mouseup', 'click'].forEach(type => {
                    const event = new MouseEvent(type, eventOpts);
                    btn.dispatchEvent(event);
                });

                console.log('✅ 강제 클릭 이벤트 시도 완료');
            })();
    "

        Dim js As String = scriptTemplate.
        Replace("__PARTIAL_URL__", partialUrl.Replace("""", "\"""))

        'Dim result As String = Await WebView21.CoreWebView2.ExecuteScriptAsync(js)
        'Return result.Trim(""""c) ' " 제거

        If WebView21.InvokeRequired Then
            Return Await CType(WebView21.Invoke(
                New Func(Of Task(Of String))(
                    Async Function()
                        Dim result As String = Await WebView21.CoreWebView2.ExecuteScriptAsync(js)
                        Return result.Trim(""""c)
                    End Function)), Task(Of String))
        Else
            Dim result As String = Await WebView21.CoreWebView2.ExecuteScriptAsync(js)
            Return result.Trim(""""c)
        End If
    End Function

    Public Async Function SendWebSocketMessageAsync(partialUrl As String, message As String) As Task(Of String)
        Dim scriptTemplate As String = "
        (function() {
            const partialUrl = '__PARTIAL_URL__';
            const message = '__MESSAGE__';
            const list = window.__wsList || [];
            for (let i = 0; i < list.length; i++) {
                const ws = list[i];
                if (ws.url.includes(partialUrl) && ws.readyState === WebSocket.OPEN) {
                    ws.send(message);
                    return 'success';
                }
            }
            return 'not_found';
        })();
    "

        Dim js As String = scriptTemplate.
        Replace("__PARTIAL_URL__", partialUrl.Replace("""", "\""")).
        Replace("__MESSAGE__", message.Replace("""", "\"""))

        'Dim result As String = Await WebView21.CoreWebView2.ExecuteScriptAsync(js)
        'Return result.Trim(""""c) ' " 제거

        If WebView21.InvokeRequired Then
            Return Await CType(WebView21.Invoke(
                New Func(Of Task(Of String))(
                    Async Function()
                        Dim result As String = Await WebView21.CoreWebView2.ExecuteScriptAsync(js)
                        Return result.Trim(""""c)
                    End Function)), Task(Of String))
        Else
            Dim result As String = Await WebView21.CoreWebView2.ExecuteScriptAsync(js)
            Return result.Trim(""""c)
        End If
    End Function

    Private Async Function SubscribeToWebSocketEventsAsync(coreWebView2 As CoreWebView2) As Task
        Try
            Dim helper As DevToolsProtocolHelper = coreWebView2.GetDevToolsProtocolHelper()

            Try
                Await helper.Target.SetAutoAttachAsync(autoAttach:=True, waitForDebuggerOnStart:=False, flatten:=True)
                'LogMessage("Target.SetAutoAttachAsync(flatten:=True) 호출 성공")
            Catch exAutoAttach As Exception
                'LogMessage($"Target.SetAutoAttachAsync 호출 오류: {exAutoAttach.Message}")
            End Try

            Await helper.Network.EnableAsync()
            Await helper.Page.EnableAsync() ' Page.FrameNavigated 이벤트 수신 및 프레임 정보 수집에 필요

            Dim wsCreatedReceiver As CoreWebView2DevToolsProtocolEventReceiver = coreWebView2.GetDevToolsProtocolEventReceiver("Network.webSocketCreated")
            AddHandler wsCreatedReceiver.DevToolsProtocolEventReceived, Sub(sender_core, args_core)
                                                                            Try
                                                                                Dim payload As CdpWebSocketCreatedPayload = System.Text.Json.JsonSerializer.Deserialize(Of CdpWebSocketCreatedPayload)(args_core.ParameterObjectAsJson)
                                                                                If payload IsNot Nothing AndAlso payload.requestId IsNot Nothing Then
                                                                                    SyncLock _SyncLock
                                                                                        Dim webSocketInfo As New WebSocketInfo With {
                            .RequestId = payload.requestId,
                            .Url = payload.url,
                            .CreatedTime = DateTime.Now,
                            .FrameId = payload.frameId ' 여기서 FrameId를 정확히 가져옴
                        }
                                                                                        webSockets(payload.requestId) = webSocketInfo
                                                                                    End SyncLock
                                                                                End If
                                                                            Catch exJson As System.Text.Json.JsonException
                                                                                'LogMessage($"JSON Deserialization Error for Network.webSocketCreated (Core Event): {exJson.Message} - JSON: {args_core.ParameterObjectAsJson}")
                                                                            Catch exProc As Exception
                                                                                'LogMessage($"Error processing Network.webSocketCreated (Core Event): {exProc.Message}")
                                                                            End Try
                                                                        End Sub

            AddHandler helper.Network.WebSocketHandshakeResponseReceived, Sub(s, e_hs)
                                                                              SyncLock _SyncLock
                                                                                  If webSockets.ContainsKey(e_hs.RequestId) Then
                                                                                      ' <<< URL 필터링 로직 추가 시작 >>>
                                                                                      Dim targetUrlPart As String = "public/powerball" ' 여기에 필터링할 URL의 특정 부분을 입력하세요.
                                                                                      If webSockets(e_hs.RequestId).Url.Contains(targetUrlPart) Then
                                                                                          webSockets(e_hs.RequestId).Headers = e_hs.Response.Headers ' Headers는 Object 타입이므로, 실제 사용 시 적절한 캐스팅이나 파싱 필요

                                                                                          MaterialTabControl1.SelectTab(TabPage2)
                                                                                          Timer1.Enabled = True
                                                                                      Else
                                                                                      End If
                                                                                      ' <<< URL 필터링 로직 추가 끝 >>>
                                                                                  Else
                                                                                  End If
                                                                              End SyncLock
                                                                          End Sub

            AddHandler helper.Network.WebSocketFrameReceived, Sub(s, e_fr)
                                                                  SyncLock _SyncLock
                                                                      If webSockets.ContainsKey(e_fr.RequestId) Then
                                                                          ' <<< URL 필터링 로직 추가 시작 >>>
                                                                          Dim targetUrlPart As String = "public/powerball" ' 여기에 필터링할 URL의 특정 부분을 입력하세요.
                                                                          If webSockets(e_fr.RequestId).Url.Contains(targetUrlPart) Then
                                                                              webSockets(e_fr.RequestId).Frames.Add($"수신: {e_fr.Response.PayloadData}")
                                                                              Task.Run(New Action(Sub()
                                                                                                      Debug.Print(String.Format("RECV - {0}", e_fr.Response.PayloadData))
                                                                                                      cLogManager.Instance.AddToLog(String.Format("[RECV] {0}", e_fr.Response.PayloadData))
                                                                                                      If InStr(e_fr.Response.PayloadData, "connectionAlreadyExists") > 0 Then
                                                                                                          MsgBox("중복접속 중")
                                                                                                      End If
                                                                                                      RunTask(e_fr.Response.PayloadData)
                                                                                                  End Sub))

                                                                          Else
                                                                          End If
                                                                      Else
                                                                      End If
                                                                  End SyncLock
                                                              End Sub

            AddHandler helper.Network.WebSocketFrameSent, Sub(s, e_fs)
                                                              SyncLock _SyncLock
                                                                  If webSockets.ContainsKey(e_fs.RequestId) Then
                                                                      ' <<< URL 필터링 로직 추가 시작 >>>
                                                                      Dim targetUrlPart As String = "public/powerball" ' 여기에 필터링할 URL의 특정 부분을 입력하세요.
                                                                      If webSockets(e_fs.RequestId).Url.Contains(targetUrlPart) Then
                                                                          webSockets(e_fs.RequestId).Frames.Add($"전송: {e_fs.Response.PayloadData}")

                                                                          Task.Run(New Action(Sub()
                                                                                                  Debug.Print(String.Format("SEND - {0}", e_fs.Response.PayloadData))
                                                                                                  cLogManager.Instance.AddToLog(String.Format("[SEND] {0}", e_fs.Response.PayloadData))
                                                                                              End Sub))
                                                                      Else
                                                                      End If
                                                                  Else
                                                                  End If
                                                              End SyncLock
                                                          End Sub

            AddHandler helper.Page.FrameNavigated, Sub(sender_fn, e_fn)
                                                       SyncLock _SyncLock
                                                           frames(e_fn.Frame.Id) = New FrameInfo With {
                    .FrameId = e_fn.Frame.Id,
                    .ParentFrameId = If(String.IsNullOrEmpty(e_fn.Frame.ParentId), Nothing, e_fn.Frame.ParentId),
                    .Url = e_fn.Frame.Url,
                    .Name = e_fn.Frame.Name
                }
                                                       End SyncLock
                                                   End Sub

        Catch ex As Exception
            ' 구독 설정 중 발생하는 최상위 예외 처리
        End Try
    End Function

    Private Sub CoreWebView2_FrameCreated(sender As Object, e As CoreWebView2FrameCreatedEventArgs)
        Try
            Dim ownerCoreWebView2 As CoreWebView2 = TryCast(sender, CoreWebView2)
            If ownerCoreWebView2 Is Nothing Then
                Return
            End If

            Dim frameDisplayName As String = If(Not String.IsNullOrEmpty(e.Frame.Name), e.Frame.Name, $"[이름 없는 프레임-{Guid.NewGuid().ToString("N").Substring(0, 6)}]")

            AddHandler e.Frame.NavigationStarting, Sub(s, navStartingArgs)
                                                       Dim frameOwnerDescription As String = "알 수 없는 소유자"
                                                       Dim tempPopUpWindowIdForLog As String = Nothing

                                                       If ownerCoreWebView2 Is WebView21.CoreWebView2 Then ' 메인 webView의 프레임인지 확인
                                                           frameOwnerDescription = "메인 WebView"
                                                       Else ' 새 창(팝업)의 프레임인지 확인
                                                           SyncLock _SyncLock
                                                               For Each kvp As KeyValuePair(Of String, NewWindowInfo) In newWindows
                                                                   If kvp.Value.WebView IsNot Nothing AndAlso kvp.Value.WebView.CoreWebView2 Is ownerCoreWebView2 Then
                                                                       frameOwnerDescription = $"새 창 (ID: {kvp.Value.WindowId})"
                                                                       tempPopUpWindowIdForLog = kvp.Value.WindowId
                                                                       Exit For
                                                                   End If
                                                               Next
                                                           End SyncLock
                                                           If tempPopUpWindowIdForLog Is Nothing Then frameOwnerDescription = "분리된/알수없는 WebView"
                                                       End If
                                                   End Sub

            AddHandler e.Frame.NavigationCompleted, Sub(s, navCompletedArgs)
                                                        Dim frameOwnerDescription As String = "알 수 없는 소유자"
                                                        Dim tempPopUpWindowIdForLog As String = Nothing
                                                        If ownerCoreWebView2 Is WebView21.CoreWebView2 Then
                                                            frameOwnerDescription = "메인 WebView"
                                                        Else
                                                            SyncLock _SyncLock
                                                                For Each kvp As KeyValuePair(Of String, NewWindowInfo) In newWindows
                                                                    If kvp.Value.WebView IsNot Nothing AndAlso kvp.Value.WebView.CoreWebView2 Is ownerCoreWebView2 Then
                                                                        frameOwnerDescription = $"새 창 (ID: {kvp.Value.WindowId})"
                                                                        tempPopUpWindowIdForLog = kvp.Value.WindowId
                                                                        Exit For
                                                                    End If
                                                                Next
                                                            End SyncLock
                                                            If tempPopUpWindowIdForLog Is Nothing Then frameOwnerDescription = "분리된/알수없는 WebView"
                                                        End If
                                                    End Sub

            Dim specificRedirectUrlInFrame As String = "/frontend/"
            'Dim specificRedirectUrlInFrame As String = "/frontend/evo/r2/"

            Dim popUpFormToClose As Form = Nothing
            Dim popUpWindowIdForAction As String = Nothing ' 작업을 수행할 팝업 창의 ID

            ' 이 프레임이 팝업 창에 속하는지 확인
            SyncLock _SyncLock ' newWindows 컬렉션은 여러 스레드에서 접근될 수 있으므로 동기화
                For Each kvp As KeyValuePair(Of String, NewWindowInfo) In newWindows
                    If kvp.Value.WebView IsNot Nothing AndAlso kvp.Value.WebView.CoreWebView2 Is ownerCoreWebView2 Then
                        ' ownerCoreWebView2 (이 프레임을 생성한 WebView)가 팝업 창의 WebView 중 하나와 일치함
                        popUpFormToClose = kvp.Value.Form
                        popUpWindowIdForAction = kvp.Value.WindowId
                        Exit For
                    End If
                Next
            End SyncLock

            ' popUpFormToClose가 Nothing이 아니라면, 이 프레임은 팝업 창 내부에 있는 것입니다.
            If popUpFormToClose IsNot Nothing Then
                ' 해당 프레임(e.Frame)의 NavigationStarting 이벤트에 핸들러를 추가합니다.
                AddHandler e.Frame.NavigationStarting,
                Sub(frameNavSender As Object, frameNavArgs As CoreWebView2NavigationStartingEventArgs)

                    If frameNavArgs.Uri IsNot Nothing AndAlso
                       frameNavArgs.Uri.Contains(specificRedirectUrlInFrame) Then
                        frameNavArgs.Cancel = True

                        Dim urlToNavigateInMain As String = frameNavArgs.Uri ' 메인 창에서 열 URL 저장

                        Me.Invoke(Sub()
                                      If WebView21 IsNot Nothing AndAlso WebView21.CoreWebView2 IsNot Nothing Then
                                          WebView21.CoreWebView2.Navigate(urlToNavigateInMain)
                                      Else
                                      End If
                                  End Sub)

                        Me.Invoke(Sub()
                                      If popUpFormToClose IsNot Nothing AndAlso Not popUpFormToClose.IsDisposed Then
                                          popUpFormToClose.Close()
                                      End If
                                  End Sub)
                    End If
                End Sub
            End If

            Dim frameId As String = e.Frame.Name ' 원본 코드에서 사용한 방식
            If String.IsNullOrEmpty(frameId) Then frameId = frameDisplayName ' 이름 없는 경우 대체

            SyncLock _SyncLock
                If Not frames.ContainsKey(frameId) Then
                    frames(frameId) = New FrameInfo With {
                    .FrameId = frameId, ' 주의: 이것은 CDP FrameId와 다를 수 있음
                    .Name = e.Frame.Name,
                    .Url = String.Empty ' URL은 NavigationStarting/Completed에서 채워질 수 있음
                }
                End If
            End SyncLock
            ' --- 기존: 프레임 정보 저장 로직 끝 ---

        Catch ex As Exception
        End Try
    End Sub

    Private Sub CoreWebView2_FrameNavigationStarting(sender As Object, e As CoreWebView2NavigationStartingEventArgs)
        ' 프레임 탐색 시작 이벤트 처리
    End Sub

    Private Async Sub CoreWebView2_FrameNavigationCompleted(sender As Object, e As CoreWebView2NavigationCompletedEventArgs)
        ' 프레임 탐색 완료 이벤트 처리
        Await InjectWebSocketHookAsync()
    End Sub

    Private Async Sub CoreWebView2_NewWindowRequested(sender As Object, e As CoreWebView2NewWindowRequestedEventArgs)
        Dim deferral As CoreWebView2Deferral = Nothing
        Dim newWindowForm As Form = Nothing ' Finally 및 Catch 블록에서 접근 가능하도록 선언
        Dim windowId As String = String.Empty ' windowId를 Try 블록 상단에서 접근 가능하도록 선언

        Try
            deferral = e.GetDeferral()
            e.Handled = True

            ' 새 창의 고유 ID 생성
            windowId = Guid.NewGuid().ToString()

            newWindowForm = New Form With {
            .Text = $"새 창 ({windowId}) - {e.Uri}", ' 제목에 URL 및 ID 포함
            .Width = 800,
            .Height = 600,
            .StartPosition = FormStartPosition.CenterScreen
        }

            Dim newWebView As New WebView2 With {
            .Dock = DockStyle.Fill
        }

            newWindowForm.Controls.Add(newWebView)

            ' 새 창이 닫힐 때 newWindows 컬렉션에서 제거하는 핸들러
            AddHandler newWindowForm.FormClosed, Sub(formSender, formArgs)
                                                     Dim closedForm = DirectCast(formSender, Form)
                                                     Dim keyToRemove As String = Nothing
                                                     SyncLock _SyncLock
                                                         ' newWindows 컬렉션에서 해당 Form을 가진 항목 찾기
                                                         For Each kvp As KeyValuePair(Of String, NewWindowInfo) In newWindows
                                                             If kvp.Value.Form Is closedForm Then
                                                                 keyToRemove = kvp.Key
                                                                 Exit For
                                                             End If
                                                         Next
                                                         If keyToRemove IsNot Nothing Then
                                                             newWindows.Remove(keyToRemove)
                                                         End If
                                                     End SyncLock
                                                 End Sub

            Await newWebView.EnsureCoreWebView2Async(Nothing)

            e.NewWindow = newWebView.CoreWebView2 ' WebView2가 e.Uri로 탐색하도록 설정

            ' 새 창 정보 저장
            Dim newWindowInfo As New NewWindowInfo With {
            .WebView = newWebView,
            .Form = newWindowForm,
            .WindowId = windowId
        }
            SyncLock _SyncLock
                newWindows(windowId) = newWindowInfo
            End SyncLock

            Await SubscribeToWebSocketEventsAsync(newWebView.CoreWebView2)
            AddHandler newWebView.CoreWebView2.FrameCreated, AddressOf CoreWebView2_FrameCreated
            ' 새 폼 표시
            newWindowForm.Show()

        Catch ex As Exception
            If newWindowForm IsNot Nothing AndAlso Not newWindowForm.IsDisposed Then
                Try
                    ' 오류 발생 시 생성된 폼을 닫도록 시도
                    newWindowForm.Close()
                Catch closeEx As Exception
                End Try
            End If
            If e IsNot Nothing Then
                e.NewWindow = Nothing
            End If
        Finally
            If deferral IsNot Nothing Then
                deferral.Complete()
            End If
        End Try
    End Sub

#End Region
#Region "[금액 컨트롤 변경]"
    Dim thistotLock As New Object
    Dim thisamonutLock As New Object
    Private Sub txt시작금액_TextChanged(sender As Object, e As EventArgs) Handles txt시작금액.TextChanged
        txt시작금액.Text = String.Format("{0:#,0}", CLng(txt시작금액.Text))
        txt시작금액.Select(txt시작금액.Text.Length, 0)
    End Sub

    Private Sub txt시작금액_KeyPress(sender As Object, e As KeyPressEventArgs) Handles txt시작금액.KeyPress
        If Not Char.IsControl(e.KeyChar) AndAlso Not Char.IsDigit(e.KeyChar) Then
            e.Handled = True ' 입력 막기
        End If
    End Sub
    Public Sub SetAmountWS()
        txt수익금액.Text = CLng(txt현재금액.Text) - CLng(txt시작금액.Text)
    End Sub

    Public Sub SetAmount(val As Integer)
        SyncLock thisamonutLock
            txt수익금액.Text += CLng(val)
            txt현재금액.Text = CLng(txt시작금액.Text) + CLng(txt수익금액.Text)
        End SyncLock
    End Sub

    Public Sub SetTotalAmount(val As Integer)
        SyncLock thistotLock
            txt총배팅금액.Text += CLng(val)
        End SyncLock
    End Sub

    Public Sub ResetAmount()
        txt현재금액.Text = txt시작금액.Text
        txt수익금액.Text = 0
        txt총배팅금액.Text = 0
    End Sub

    Public Sub ResetAmount2()
        txt현재금액.Text = txt숨은리얼.Text
        txt시작금액.Text = txt현재금액.Text
        txt수익금액.Text = 0
    End Sub

    Private Sub txt현재금액_TextChanged(sender As Object, e As EventArgs) Handles txt현재금액.TextChanged
        txt현재금액.Text = String.Format("{0:#,0}", CLng(txt현재금액.Text))
        txt현재금액.Select(txt현재금액.Text.Length, 0)

        If rd가상.Checked = False Then
            If IsNumeric(txt시작금액.Text) AndAlso IsNumeric(txt현재금액.Text) Then
                txt수익금액.Text = CLng(txt현재금액.Text) - CLng(txt시작금액.Text)
            End If
        End If
    End Sub

    Private Sub txt수익금액_TextChanged(sender As Object, e As EventArgs) Handles txt수익금액.TextChanged
        Try
            txt수익금액.Text = String.Format("{0:#,0}", CLng(txt수익금액.Text))
            txt수익금액.Select(txt수익금액.Text.Length, 0)

            If CLng(txt수익금액.Text) = 0 Then
                txt수익금액.ForeColor = Color.Black
            ElseIf CLng(txt수익금액.Text) > 0 Then
                txt수익금액.ForeColor = Color.Red
            Else
                txt수익금액.ForeColor = Color.Blue
            End If
        Catch ex As Exception

        End Try

    End Sub

    Private Sub txt총배팅금액_TextChanged(sender As Object, e As EventArgs) Handles txt총배팅금액.TextChanged
        txt총배팅금액.Text = String.Format("{0:#,0}", CLng(txt총배팅금액.Text))
        txt총배팅금액.Select(txt총배팅금액.Text.Length, 0)
        If IsNumeric(txtRolRate.Text) AndAlso CLng(txtRolRate.Text) > 0 Then
            txt롤링금액.Text = String.Format("{0:#,0}", CLng(txt총배팅금액.Text) * CLng(txtRolRate.Text) / 100)
        End If
    End Sub
#End Region
#Region "[유틸]"

    Dim t2 As System.Threading.Thread
    Public Sub CountRoom()

        t2 = New System.Threading.Thread(AddressOf CountRoomP)
        t2.Start()

    End Sub
    Private Sub CountRoomP()

        Task.Run(New Action(Sub()
                                lblTimer.Visible = True
                                For ii = 40 To 1 Step -1
                                    lblTimer.Text = ii
                                    System.Threading.Thread.Sleep(1000)
                                    If lblTimer.Text = 1 Then
                                        System.Threading.Thread.Sleep(1000)
                                        lblTimer.Visible = False
                                    End If
                                Next
                            End Sub))
    End Sub

    Public Sub SetListbox(str As String)
        ListBox1.Items.Add(str)

        If ListBox1.Items.Count > 1000 Then
            ' 가장 오래된(가장 위쪽에 있는) 데이터를 삭제합니다.
            ListBox1.Items.RemoveAt(0)
        End If

        ListBox1.TopIndex = ListBox1.Items.Count - 1
    End Sub

    Public Sub SetListboxRange(str As String())
        ListBox1.Items.AddRange(str)

        If ListBox1.Items.Count > 1000 Then
            For i = ListBox1.Items.Count - 1000 To 1 Step -1
                ' 가장 오래된(가장 위쪽에 있는) 데이터를 삭제합니다.
                ListBox1.Items.RemoveAt(0)
            Next
        End If

        ListBox1.TopIndex = ListBox1.Items.Count - 1
    End Sub


    Private Function chkPattern(_str As String) As List(Of HS_Game_Pattern)

        Dim ret As List(Of HS_Game_Pattern) = New List(Of HS_Game_Pattern)

        Try
            Dim sArr() As String

            _str = Replace(_str, " ", "")

            sArr = Split(_str, vbCrLf)

            If sArr.Count = 1 AndAlso sArr.First = "" Then
                Return ret
            End If

            For i = 0 To sArr.Count - 1
                If sArr(i) <> "" Then
                    Dim List_Money As New List(Of String)
                    Dim List_Pattern As New List(Of String)

                    Dim matin() As String = Split(sArr(i), "/")

                    Dim _money As New List(Of HS_Money)

                    For ii = 0 To matin.Count - 1
                        Dim wlLevel() As String = Split(matin(ii), "=")
                        Dim temp() As String = Split(wlLevel.First, ":")

                        Dim _movelevel As New HS_MoveLevel("", "")

                        If wlLevel.Count = 2 Then
                            _movelevel = New HS_MoveLevel(Split(wlLevel.Last, ":").First, Split(wlLevel.Last, ":").Last)
                        End If

                        If _movelevel.wLevel <> "" AndAlso InStr(_movelevel.wLevel, "-") = 0 Then
                            _movelevel.wLevel = _movelevel.wLevel & "-1"
                        End If

                        If _movelevel.lLevel <> "" AndAlso InStr(_movelevel.lLevel, "-") = 0 Then
                            _movelevel.lLevel = _movelevel.lLevel & "-1"
                        End If

                        Dim _pattern As New List(Of HS_Pattern)
                        For j = 1 To temp.Count - 1
                            Dim _temp_j() As String = Split(temp(j), "-")
                            'Dim str As String = Replace(Replace(Replace(temp(j), "1", "B"), "2", "P"), "3", "T")
                            'Dim _temp_j() As String = Split(str, "-")
                            Dim _pattern_J As New HS_Pattern(_temp_j.First, _temp_j.Last)
                            _pattern.Add(_pattern_J)
                        Next
                        Dim _pattern_ii As New HS_Money(temp.First, _pattern, _movelevel)
                        _money.Add(_pattern_ii)
                    Next
                    ret.Add(New HS_Game_Pattern(_money))
                End If
            Next

        Catch ex As Exception
            Return ret
        End Try

        Return ret

    End Function

    Private Function chkPattern_Multi(_str As String) As List(Of HS_Game_Line_Data)

        Dim ret As New List(Of HS_Game_Line_Data)

        Try
            Dim sArr() As String

            _str = _str.Replace(" ", "")

            sArr = _str.Split({Environment.NewLine}, StringSplitOptions.RemoveEmptyEntries)

            If sArr.Length = 0 Then
                Return ret
            End If

            For i = 0 To sArr.Length - 1
                Dim line = sArr(i)
                If Not String.IsNullOrWhiteSpace(line) Then
                    Dim gamePatternsInLine As New List(Of HS_Game_Pattern_Multi)

                    ' '/'를 기준으로 하나의 라인을 여러 개의 게임 패턴 블록으로 분리
                    Dim gamePatternBlocks = line.Split("/"c)

                    For Each block In gamePatternBlocks
                        Dim parts = block.Split("@"c)
                        If parts.Length < 1 Then Continue For

                        Dim currentAmount As Decimal = 0
                        If Decimal.TryParse(parts(0), currentAmount) Then
                            ' 금액 파싱 성공
                        Else
                            ' 금액 파싱 실패 시 처리 (예: 건너뛰거나 오류 기록)
                            Continue For
                        End If

                        Dim moneyPatterns As New List(Of HS_Money_Multi)

                        For j = 1 To parts.Length - 1
                            Dim rulePart = parts(j)
                            Dim ruleSubParts = rulePart.Split("="c)
                            If ruleSubParts.Length <> 2 Then Continue For

                            Dim key As String = ruleSubParts(0)
                            Dim valueStrings = ruleSubParts(1).Split(":"c)

                            Dim patterns As New List(Of HS_Pattern_Multi)
                            For Each valStr In valueStrings
                                Dim patternParts = valStr.Split("-"c)
                                If patternParts.Length = 2 Then
                                    patterns.Add(New HS_Pattern_Multi(patternParts(0), patternParts(1)))
                                End If
                            Next
                            moneyPatterns.Add(New HS_Money_Multi(key, patterns))
                        Next
                        gamePatternsInLine.Add(New HS_Game_Pattern_Multi(currentAmount, moneyPatterns))
                    Next
                    ret.Add(New HS_Game_Line_Data(gamePatternsInLine))
                End If
            Next

        Catch ex As Exception
            ' 예외 처리 로직 (필요에 따라 로깅 등 추가)
            Return New List(Of HS_Game_Line_Data) ' 오류 발생 시 빈 리스트 반환
        End Try

        Return ret

    End Function

    Private Function GetAmount(pat As List(Of HS_Game_Pattern), str As String) As Long
        Dim ret As Integer = 0

        Dim sArr() As String = Split(str, "-")

        Return CLng(pat(sArr.First - 1).money_list(sArr.Last - 1).money)

    End Function

    Private Function GetAmount_Multi(pat As List(Of HS_Game_Line_Data), str As String) As Long
        Dim ret As Integer = 0

        Dim sArr() As String = Split(str, "-")

        Return CLng(pat(sArr.First - 1).GamePatterns(sArr.Last - 1).Amount)

    End Function

    Private Function TruncateString(input As String, maxLength As Integer) As String
        If String.IsNullOrEmpty(input) Then
            Return String.Empty
        End If

        If input.Length <= maxLength Then
            Return input
        End If

        Return input.Substring(0, maxLength) & "..."
    End Function
    Public Function FindMatchingRowIndexes(mesu As String, gametype As String, gametype2 As String, gametype3 As String) As List(Of Integer)
        SyncLock dataLock
            Dim result As New List(Of Integer)
            For i As Integer = 0 To lst단계별통계Data.Count - 1
                Dim item = lst단계별통계Data(i)
                If item.Length >= 5 AndAlso
                   item(1) = mesu AndAlso
                   item(2) = gametype AndAlso
                   item(3) = gametype2 AndAlso
                   item(4) = gametype3 Then
                    result.Add(i)
                End If
            Next
            Return result
        End SyncLock
    End Function

    Private Sub UpdateMaxLevel(bList As List(Of _Bet))
        ' Optimized version without UI thread blocking
        Task.Run(Sub()
                     For iii As Integer = 0 To bList.Count - 1
                            Dim _gametype As String = ""
                            Dim _gametype2 As String = ""
                            Dim _gametype3 As String = ""
                            Dim _mesu As String = ""

                            Select Case bList(iii)._BetType
                                Case 0
                                    _gametype = bList(iii)._GameType
                                    _gametype2 = bList(iii)._GameType2
                                Case 1
                                    _gametype3 = bList(iii)._GameType
                                    _gametype2 = bList(iii)._GameType2
                            End Select

                            _mesu = bList(iii)._Mesu

                            Dim indexes = FindMatchingRowIndexes(_mesu, _gametype, _gametype2, _gametype3)

                            For Each idx In indexes
                                Dim itemData = GetVirtual단계별통계Item(idx)
                                Dim _level As String = itemData(5)
                                Dim _amount As String = itemData(6)
                                Dim Arr1() As String = Split(_level, "-")
                                Dim Arr2() As String = Split(bList(iii)._Level, "-")
                                Dim SetVal As String = _level

                                If CInt(Arr2.First) > CInt(Arr1.First) Then
                                    SetVal = bList(iii)._Level
                                ElseIf CInt(Arr2.First) = CInt(Arr1.First) Then
                                    If CInt(Arr2.Last) >= CInt(Arr1.Last) Then
                                        SetVal = bList(iii)._Level
                                    End If
                                End If

                                SetVirtual단계별통계SubItem(idx, 5, SetVal)

                                If bList(iii)._Amount > CLng(_amount) Then
                                    SetVirtual단계별통계SubItem(idx, 6, bList(iii)._Amount.ToString())
                                End If
                            Next
                     Next
                 End Sub)

    End Sub

    Private Function AddPatList(sBetType As String, s패턴 As String, s단계 As String,
                           s승무패 As String, gametype As String, gametype2 As String, p_reckey As Integer, mesu As String) As ListViewItem
        'Add items in the listview
        Dim arr(10) As String
        Dim itm As New ListViewItem

        'Add first item
        arr(0) = sBetType
        arr(1) = s패턴
        arr(2) = mesu
        arr(3) = s단계
        arr(4) = s승무패
        arr(5) = ""
        arr(6) = ""
        arr(7) = 0
        arr(8) = gametype
        arr(9) = gametype2
        arr(10) = p_reckey
        itm = New ListViewItem(arr)
        'lst패턴설정.Items.Add(itm)

        Return itm
    End Function

    'Public Function CreateGroupedListFromText(inputText As String, maxGroupSize As Integer) As List(Of mesu_char_class)

    '    ' 최종 결과를 담을 리스트
    '    Dim result_list_all As New List(Of mesu_char_class)
    '    ' 입력 문자열을 문자 배열로 변환
    '    Dim inputChars() As Char = inputText.ToCharArray()

    '    ' 2개 그룹부터 maxGroupSize 그룹까지 시도
    '    For groupSize = maxGroupSize To maxGroupSize

    '        ' 각 그룹 인덱스 (0부터 시작)
    '        For groupIndex = 0 To groupSize - 1

    '            ' 현재 그룹에 속할 문자들을 담을 임시 리스트
    '            Dim current_char_list As New List(Of String)

    '            ' 전체 문자열을 순회하며 현재 그룹에 속하는 문자를 찾음
    '            For j = 0 To inputChars.Length - 1
    '                ' 문자의 '위치(j)'를 '그룹 크기(groupSize)'로 나눈 나머지가
    '                ' 현재 '그룹 인덱스(groupIndex)'와 같으면 같은 그룹임
    '                If j Mod groupSize = groupIndex Then
    '                    current_char_list.Add(inputChars(j))
    '                End If
    '            Next

    '            ' 그룹에 문자가 하나라도 있다면 결과 리스트에 추가
    '            If current_char_list.Count > 0 Then
    '                Dim new_group = New mesu_char_class(groupSize, groupIndex, current_char_list)
    '                result_list_all.Add(new_group)
    '            End If
    '        Next
    '    Next

    '    Return result_list_all
    'End Function
    Public Function CreateGroupedListFromText(inputText As String, maxGroupSize As Integer) As List(Of mesu_char_class)
        Dim resultList As New List(Of mesu_char_class)
        Dim chars() As Char = inputText.ToCharArray()

        For groupIndex = 0 To maxGroupSize - 1
            Dim groupChars As New List(Of Char)

            ' Mod 없이 Step으로 바로 계산
            For j = groupIndex To chars.Length - 1 Step maxGroupSize
                groupChars.Add(chars(j))
            Next

            If groupChars.Count > 0 Then
                resultList.Add(New mesu_char_class(maxGroupSize, groupIndex, groupChars))
            End If
        Next

        Return resultList
    End Function
#End Region
    Private Sub ClickAtPointSafe(x As Integer, y As Integer)
        If WebView21.InvokeRequired Then
            WebView21.Invoke(Sub()
                                 Dim z = ClickAtPoint(x, y) ' 비동기 함수는 무시하거나 Task.Run 처리
                             End Sub)
        Else
            Dim z = ClickAtPoint(x, y)
        End If
    End Sub

    Private Async Function ClickAtPoint(x As Integer, y As Integer) As Task

        Dim downEvent = "{
        ""type"": ""mousePressed"",
        ""x"": " & x & ",
        ""y"": " & y & ",
        ""button"": ""left"",
        ""clickCount"": 1
    }"
        Dim upEvent = "{
        ""type"": ""mouseReleased"",
        ""x"": " & x & ",
        ""y"": " & y & ",
        ""button"": ""left"",
        ""clickCount"": 1
    }"

        Await WebView21.CoreWebView2.CallDevToolsProtocolMethodAsync("Input.dispatchMouseEvent", downEvent)
        Await WebView21.CoreWebView2.CallDevToolsProtocolMethodAsync("Input.dispatchMouseEvent", upEvent)

    End Function

    Private Sub RunTask(str As String)

        Try
            Dim _data As cJsonGet.WS_Multi_Read.Rootobject = Newtonsoft.Json.JsonConvert.DeserializeObject(Of cJsonGet.WS_Multi_Read.Rootobject)(str)

            Select Case _data.type
                Case "balanceUpdated"
                    Task.Run(New Action(Sub()
                                            Try
                                                Dim _bg As cJsonGet.WS_Multi_balanceUpdated.Rootobject = Newtonsoft.Json.JsonConvert.DeserializeObject(Of cJsonGet.WS_Multi_balanceUpdated.Rootobject)(str)

                                                If rd가상.Checked = False Then
                                                    If Button7.Text = "배팅시작" Then
                                                        txt시작금액.Text = _bg.args.balance
                                                        txt현재금액.Text = _bg.args.balance
                                                    Else
                                                        txt현재금액.Text = _bg.args.balance
                                                        SetAmountWS()
                                                    End If
                                                End If
                                                txt숨은리얼.Text = _bg.args.balance
                                            Catch ex As Exception
                                            End Try
                                        End Sub))
                Case "powerball.player.gameState"
                    Task.Run(New Action(Sub()
                                            Try
                                                Dim _bg As cJsonGet.WS_Multi_powerball_player_gameState.Rootobject = Newtonsoft.Json.JsonConvert.DeserializeObject(Of cJsonGet.WS_Multi_powerball_player_gameState.Rootobject)(str)

                                                If _bg.args.stage = "Betting" Then
                                                    _cur_game = _bg
                                                    'If DateDiff(DateInterval.Minute, _lastBet, Now) > 10 AndAlso rd가상.Checked Then
                                                    '    If WebView21.InvokeRequired Then
                                                    '        WebView21.Invoke(Sub()
                                                    '                             Set_Virtual()
                                                    '                             WebView21.Reload()
                                                    '                             ListBox2.Items.Add("가상배팅 재시작: " & Now.ToString("yyyy-MM-dd HH:mm:ss"))
                                                    '                         End Sub)
                                                    '    Else
                                                    '        WebView21.Reload()
                                                    '    End If
                                                    '    _lastBet = Now
                                                    'End If
                                                    'ClickAtPointSafe(1200, 40)
                                                    'If zzzz Then
                                                    '    zzzz = False
                                                    'Else
                                                    '    zzzz = True
                                                    'End If
                                                    'Task.Run(Async Sub()
                                                    '             Await SendWebSocketMessageAsync("powerball/player/game", p_WS_History(zzzz))
                                                    '         End Sub)

                                                ElseIf _bg.args.stage = "WaitingForGame" Then
                                                    lblState.Text = "대기중"
                                                ElseIf _bg.args.stage = "AcceptingBets" Then
                                                    lblState.Text = "배팅마감"
                                                ElseIf _bg.args.stage = "DrawingRegular" Then
                                                    lblState.Text = "일반볼 뽑는 중"
                                                ElseIf _bg.args.stage = "DrawingPower" Then
                                                    lblState.Text = "파워볼 뽑는 중"
                                                ElseIf _bg.args.stage = "Resolving" Then
                                                    lblState.Text = "게임종료"
                                                End If
                                            Catch ex As Exception
                                            End Try
                                        End Sub))
                Case "powerball.player.ballsState"
                    Task.Run(New Action(Sub()
                                            Try
                                                Dim _bg As cJsonGet.WS_Multi_powerball_player_ballsState.Rootobject = Newtonsoft.Json.JsonConvert.DeserializeObject(Of cJsonGet.WS_Multi_powerball_player_ballsState.Rootobject)(str)

                                                Dim result As String = String.Join(", ", _bg.args.balls.Where(Function(ball) ball.status = "Drawn").Select(Function(ball) ball.value.ToString()).ToList())

                                                lblBall.Text = result

                                            Catch ex As Exception
                                            End Try
                                        End Sub))
                Case "powerball.player.betState"
                    Task.Run(New Action(Sub()
                                            Try
                                                Dim _bg As cJsonGet.WS_Multi_powerball_player_betState.Rootobject = Newtonsoft.Json.JsonConvert.DeserializeObject(Of cJsonGet.WS_Multi_powerball_player_betState.Rootobject)(str)

                                                If _bg.args.type = "BetReceivedByServer" AndAlso _cur_game.args.gameId <> "" Then
                                                    lblState.Text = "배팅시작"
                                                    lblBall.Text = ""
                                                    CountRoom()
                                                    Set_Open()

                                                    If DateDiff(DateInterval.Minute, _lastBet, Now) > 23 AndAlso rd가상.Checked Then

                                                        System.Threading.Thread.Sleep(5000)

                                                        ClickAtPointSafe(557, 735)

                                                        System.Threading.Thread.Sleep(1000)

                                                        ClickAtPointSafe(490, 550)

                                                        If WebView21.InvokeRequired Then
                                                            WebView21.Invoke(Sub()
                                                                                 ListBox2.Items.Add("가상배팅 재시작: " & Now.ToString("yyyy-MM-dd HH:mm:ss"))
                                                                             End Sub)
                                                        Else
                                                            'WebView21.Reload()
                                                        End If
                                                        _lastBet = Now
                                                    Else
                                                        System.Threading.Thread.Sleep(5000)

                                                        ClickAtPointSafe(557, 735)

                                                        System.Threading.Thread.Sleep(1000)

                                                        ClickAtPointSafe(490, 550)

                                                        System.Threading.Thread.Sleep(1000)

                                                        ClickAtPointSafe(510, 735)
                                                    End If

                                                    'If DateDiff(DateInterval.Minute, _lastBet, Now) > 10 AndAlso rd가상.Checked Then
                                                    '    _lastBet = Now
                                                    '    Set_Virtual(str)
                                                    'End If
                                                End If
                                            Catch ex As Exception
                                            End Try
                                        End Sub))
                Case "powerball.player.rootState"
                    Task.Run(New Action(Sub()
                                            Try
                                                If bStart = False Then
                                                    Dim _bg As cJsonGet.WS_Multi_powerball_player_rootState.Rootobject = Newtonsoft.Json.JsonConvert.DeserializeObject(Of cJsonGet.WS_Multi_powerball_player_rootState.Rootobject)(str)

                                                    _bg.args.roadsState.history.ForEach(Sub(x)
                                                                                            _GameList.Add(New HS_History(x.gameId, x.gameNumber, x.ballValues))
                                                                                        End Sub)

                                                    SetResultList(_GameList.Count - 1)
                                                    bStart = True
                                                    _lastBet = Now
                                                End If
                                            Catch ex As Exception
                                            End Try
                                        End Sub))
                Case "powerball.player.roadsState"
                    Task.Run(New Action(Sub()
                                            Try
                                                Dim _bg As cJsonGet.WS_Multi_powerball_player_roadsState.Rootobject = Newtonsoft.Json.JsonConvert.DeserializeObject(Of cJsonGet.WS_Multi_powerball_player_roadsState.Rootobject)(str)

                                                _GameList.Add(New HS_History(_bg.args.history.First.gameId,
                                                                             _bg.args.history.First.gameNumber,
                                                                             _bg.args.history.First.ballValues))

                                                SetResultList(_GameList.Count - 1)

                                                lblState.Text = "배팅마감"

                                                Set_Close(_GameList.Count - 1)
                                            Catch ex As Exception
                                            End Try
                                        End Sub))
            End Select

        Catch ex As Exception

        End Try

    End Sub

    Public Sub Set_Virtual()

        If Button7.Text = "배팅중지" Then
            Dim bSum As New List(Of _Bet)

            Dim __BetList As New List(Of _Bet)

            __BetList.Add(New _Bet(2000, "오버", cboBetType.SelectedIndex, "일반볼", "언오버", _mesu, 0, ""))

            If __BetList.Count > 0 Then
                __BetList.ForEach(Sub(x)
                                      Dim aaa = bSum.Where(Function(xx) xx._GameType = x._GameType And xx._GameType2 = x._GameType2 And xx._BetVal = x._BetVal).ToList

                                      If aaa.Count = 0 Then
                                          bSum.Add(New _Bet(x._Amount, x._BetVal, x._BetType, x._GameType, x._GameType2, 0, 0, ""))
                                      Else
                                          aaa.First._Amount = aaa.First._Amount + x._Amount
                                      End If
                                  End Sub)
            End If

            Dim bSum2 As New List(Of _Bet)

            bSum.ForEach(Async Sub(x)
                             bSum2.Add(x)
                             Await SendWebSocketMessageAsync("powerball/player/game", p_changeBetAmount(x, _cur_game.args.gameId, "1"))
                             Await SendWebSocketMessageAsync("powerball/player/game", p_Bet(x, bSum2, _cur_game.args.number, _cur_game.args.gameId))
                         End Sub)
        End If

    End Sub


#Region "[매리스트 업데이트]"
    Private Sub SetResultList(iCnt As Integer, Optional bbb As Boolean = False)

        Dim thread As New Threading.Thread(Sub()
                                               If bbb = False Then
                                                   UpdateListboxiCnt(ListBox2, iCnt)
                                               End If
                                               SetMeUpdate2(lstg1, "")
                                               SetMeUpdate2(lstg2, "Over")
                                               SetMeUpdate2(lstg3, "")
                                               SetMeUpdate2(lstp1, "")
                                               SetMeUpdate2(lstp2, "Over")
                                               SetMeUpdate(lstg4, "")
                                           End Sub)

        thread.Start()

    End Sub

    Public Sub SetMeUpdate(lst대상 As ListView, type As String)

        If lst대상.InvokeRequired Then
            lst대상.Invoke(Sub() SetMeUpdate(lst대상, type))
        Else

            Dim str As String = ""
            Dim str2 As String = ""

            str = String.Join("", _GameList.Select(Function(history) history.gOddEven))

            If chk홀짝언오버.Checked Then
                str2 = String.Join("", _GameList.Select(Function(history) history.gUnderOver))
            Else
                str2 = String.Join("", _GameList.Select(Function(history) history.gDJS))
            End If

            Dim b초과 As Boolean = False
            Dim iCnt As Integer = 100
            If str.Length > 100 Then
                b초과 = True
                Dim zz As Integer = (100 / _mesu) + _mesu * 6 + str.Length Mod _mesu
                iCnt = zz
                str = Microsoft.VisualBasic.Right(str, iCnt)
                str2 = Microsoft.VisualBasic.Right(str2, iCnt)
            End If

            Dim Arrstr = CreateGroupedListFromText(str, _mesu)
            Dim Arrstr2 = CreateGroupedListFromText(str2, _mesu)

            Task.Run(Sub()
                         lst대상.Columns.Clear()
                         lst대상.Items.Clear()
                         Dim val As Integer = Math.Abs(iCnt / _mesu) + 2   'frmMain.f.cboMesu.Text
                         lst대상.Columns.Add("", 0, HorizontalAlignment.Center)

                         For i = 1 To val - 1
                             'Add column header
                             lst대상.Columns.Add("", 16, HorizontalAlignment.Center)
                             lst대상.Columns.Add("", 16, HorizontalAlignment.Center)
                             lst대상.Columns.Add("", 3, HorizontalAlignment.Center)
                         Next

                         lst대상.BeginUpdate()

                         For j = 0 To Arrstr.Count - 1
                             Dim arr(val) As String
                             Dim itm As New ListViewItem

                             For i = 0 To val
                                 arr(i) = ""
                             Next

                             itm.UseItemStyleForSubItems = False
                             itm.Text = ""

                             For i = 0 To Arrstr(j).CharList.Count - 1
                                 Select Case Arrstr(j).CharList(i)
                                     Case "O"
                                         itm.SubItems.Add("", Color.White, Color.Blue, lst대상.Font)
                                     Case "E"
                                         itm.SubItems.Add("", Color.White, Color.Red, lst대상.Font)
                                 End Select
                                 arr(i) = Arrstr(j).CharList(i)


                                 Select Case Arrstr2(j).CharList(i)
                                     Case "J", "U"
                                         itm.SubItems.Add("", Color.White, Color.Blue, lst대상.Font)
                                     Case "D", "O"
                                         itm.SubItems.Add("", Color.White, Color.Red, lst대상.Font)
                                     Case "S"
                                         itm.SubItems.Add("", Color.White, Color.Green, lst대상.Font)
                                     Case Else
                                         itm.SubItems.Add("")
                                 End Select
                                 arr(i) = Arrstr2(j).CharList(i)

                                 itm.SubItems.Add("")
                             Next

                             lst대상.Items.Add(itm)
                         Next

                         lst대상.EndUpdate()

                     End Sub)

        End If

    End Sub

    Public Sub SetMeUpdate2(lst대상 As ListView, type As String)

        If lst대상.InvokeRequired Then
            lst대상.Invoke(Sub() SetMeUpdate2(lst대상, type))
        Else

            Dim str As String = ""

            Select Case lst대상.Name
                Case "lstg1"
                    str = String.Join("", _GameList.Select(Function(history) history.gOddEven))
                Case "lstg2"
                    str = String.Join("", _GameList.Select(Function(history) history.gUnderOver))
                Case "lstg3"
                    str = String.Join("", _GameList.Select(Function(history) history.gDJS))
                Case "lstg4"
                    str = String.Join("", _GameList.Select(Function(history) history.pOddEven))
                Case "lstp1"
                    str = String.Join("", _GameList.Select(Function(history) history.pOddEven))
                Case "lstp2"
                    str = String.Join("", _GameList.Select(Function(history) history.pUnderOver))
            End Select

            If str = "" Then
                str = " "
            End If

            Dim b초과 As Boolean = False
            Dim iCnt As Integer = 100
            If str.Length > 100 Then
                b초과 = True
                Dim zz As Integer = (100 / _mesu) + _mesu * 6 + str.Length Mod _mesu
                iCnt = zz
                str = Microsoft.VisualBasic.Right(str, iCnt)
            End If

            Dim asdasad = CreateGroupedListFromText(str, _mesu)

            Task.Run(Sub()
                         lst대상.Columns.Clear()
                         lst대상.Items.Clear()
                         Dim val As Integer = Math.Abs(iCnt / _mesu) + 2   'frmMain.f.cboMesu.Text
                         lst대상.Columns.Add("", 0, HorizontalAlignment.Center)
                         For i = 1 To val - 1
                             'Add column header
                             lst대상.Columns.Add("", 16, HorizontalAlignment.Center)
                         Next

                         lst대상.BeginUpdate()
                         asdasad.ForEach(Sub(x)
                                             Dim arr(val) As String
                                             Dim itm As New ListViewItem

                                             For i = 0 To val
                                                 arr(i) = ""
                                             Next

                                             itm.UseItemStyleForSubItems = False
                                             itm.Text = ""

                                             If type = "Over" Then
                                                 For i = 0 To x.CharList.Count - 1
                                                     Select Case x.CharList(i)
                                                         Case "U"
                                                             itm.SubItems.Add("", Color.White, Color.Blue, lst대상.Font)
                                                         Case "O"
                                                             itm.SubItems.Add("", Color.White, Color.Red, lst대상.Font)
                                                     End Select
                                                     arr(i) = x.CharList(i)
                                                 Next
                                             Else
                                                 For i = 0 To x.CharList.Count - 1
                                                     Select Case x.CharList(i)
                                                         Case "O", "J"
                                                             itm.SubItems.Add("", Color.White, Color.Blue, lst대상.Font)
                                                         Case "E", "D"
                                                             itm.SubItems.Add("", Color.White, Color.Red, lst대상.Font)
                                                         Case "S"
                                                             itm.SubItems.Add("", Color.White, Color.Green, lst대상.Font)
                                                         Case Else
                                                             itm.SubItems.Add(x.CharList(i))
                                                     End Select
                                                     arr(i) = x.CharList(i)
                                                 Next
                                             End If
                                             'itm = New ListViewItem(arr)

                                             lst대상.Items.Add(itm)

                                         End Sub
                             )

                         lst대상.EndUpdate()

                     End Sub)

        End If

    End Sub

    Private Sub UpdateListboxiCnt(lst As ListBox, icnt As Integer)
        If lst.InvokeRequired Then
            lst.Invoke(New MethodInvoker(Sub() UpdateListboxiCnt(lst, icnt)))
        Else
            Dim gameNumber As String = _GameList(icnt).gameNumber
            Dim inputTime As New DateTime(2025, 1, 1, Split(gameNumber, ":").First, Split(gameNumber, ":")(1), Split(gameNumber, ":").Last)
            Dim newTime As DateTime = inputTime.AddHours(9)
            gameNumber = newTime.ToString("HH:mm:ss")

            Dim ballList As String = String.Join(",", _GameList(icnt).ballList)
            Dim pOddEven As String = IIf(_GameList(icnt).pOddEven = "E", "짝", "홀")
            Dim pUnderOver As String = IIf(_GameList(icnt).pUnderOver = "U", "언더", "오버")
            Dim gOddEven As String = IIf(_GameList(icnt).gOddEven = "E", "짝", "홀")
            Dim gUnderOver As String = IIf(_GameList(icnt).gUnderOver = "U", "언더", "오버")
            Dim gDJS As String = IIf(_GameList(icnt).gDJS = "D", "대", IIf(_GameList(icnt).gDJS = "J", "중", "소"))
            Dim str As String = String.Format("시간:{0}, 일홀짝:{1}, 일언옵:{2}, 일대중소:{3}, 파홀짝:{4}, 파언옵:{5}, 볼결과:{6}", gameNumber, gOddEven, gUnderOver, gDJS, pOddEven, pUnderOver, ballList)
            lst.Items.Add(str)

            If lst.Items.Count > 100 Then
                ' 가장 오래된(가장 위쪽에 있는) 데이터를 삭제합니다.
                lst.Items.RemoveAt(0)
            End If

            lst.TopIndex = lst.Items.Count - 1
        End If
    End Sub
#End Region
#Region "[웹소켓 페이로드]"
    ' Network.webSocketCreated 이벤트 페이로드
    Private Class CdpWebSocketCreatedPayload
        Public Property requestId As String
        Public Property url As String
        Public Property frameId As String
        ' 필요하다면 initiator, loaderId 등 다른 속성도 추가 가능
    End Class

    ' Network.webSocketClosed 이벤트 페이로드 (requestId만 필요)
    Private Class CdpWebSocketClosedPayload
        Public Property requestId As String
    End Class

    ' Network.webSocketFrameError 이벤트 페이로드
    Private Class CdpWebSocketFrameErrorPayload
        Public Property requestId As String
        Public Property errorMessage As String
        ' timestamp 등 다른 속성도 추가 가능
    End Class

    ' Network.WebSocketHandshakeResponseReceived 이벤트 페이로드의 일부 (Headers 추출용)
    Public Class CdpWebSocketResponse
        Public Property headers As Object ' 실제로는 Dictionary(Of String, String) 등으로 파싱 가능
        Public Property status As Integer
        Public Property statusText As String
    End Class

    Public Class CdpWebSocketHandshakeResponsePayload
        Public Property requestId As String
        Public Property response As CdpWebSocketResponse
    End Class

    Private Sub MaterialRaisedButton1_Click(sender As Object, e As EventArgs) Handles MaterialRaisedButton1.Click
        _mesu = ComboBox1.Text

        SetResultList(_GameList.Count - 1, True)
    End Sub

    Private Sub chk홀짝언오버_Click(sender As Object, e As EventArgs) Handles chk홀짝언오버.Click
        SetResultList(_GameList.Count - 1, True)
    End Sub

    Private Sub RadioButton3_Click(sender As Object, e As EventArgs) Handles RadioButton3.Click
        SetResultList(_GameList.Count - 1, True)
    End Sub

#End Region
#Region "[패턴설정]"
    Private Sub cbo패턴설정_SelectedIndexChanged(sender As Object, e As EventArgs) Handles cbo패턴설정.SelectedIndexChanged

        Try
            Dim _Money As cFarePattern = New cFarePattern(cur_member.reckey)

            Dim cur_money As List(Of cFarePattern.HS_Pattern) = _Money.patternList.Where(Function(x) x.pattern_name = cbo패턴설정.Text).ToList

            txtPattern.Text = cur_money.First.pattern_value
        Catch ex As Exception

        End Try

    End Sub

    Private Sub MaterialRaisedButton2_Click(sender As Object, e As EventArgs) Handles MaterialRaisedButton2.Click
        ResetControl()
    End Sub

    Private Sub ResetControl()
        cbo패턴설정.Text = ""
        txtPattern.Text = ""
    End Sub

    Private Sub MaterialRaisedButton3_Click(sender As Object, e As EventArgs) Handles MaterialRaisedButton3.Click

        Try
            If cbo패턴설정.Text.Trim = "" Then Exit Sub
            cFarePattern.setPatternSave(cur_member.reckey, cbo패턴설정.Text.Trim, txtPattern.Text)
            Call RefreshPatternCombo()
            Call ResetControl()
            MsgBox("패턴 저장 완료")
        Catch ex As Exception

        End Try

    End Sub

    Private Sub MaterialRaisedButton4_Click(sender As Object, e As EventArgs) Handles MaterialRaisedButton4.Click

        Try
            cFarePattern.setPatternDelete(cur_member.reckey, cbo패턴설정.Text.Trim)
            Call RefreshPatternCombo()
            Call ResetControl()
        Catch ex As Exception

        End Try
    End Sub

    Private Sub RefreshPatternCombo()

        Try
            txtPattern.Text = ""
            Dim _Money As cFarePattern = New cFarePattern(cur_member.reckey)

            cbo패턴설정.Items.Clear()
            cboPatType2_1.Items.Clear()
            cboPatType2_2.Items.Clear()

            If _Money.patternList.Count > 0 Then
                _Money.patternList.ForEach(Sub(x)
                                               cbo패턴설정.Items.Add(x.pattern_name)
                                               cboPatType2_1.Items.Add(x.pattern_name)
                                               cboPatType2_2.Items.Add(x.pattern_name)
                                           End Sub)
            End If
            'cboPatType.SelectedIndex = 0
            cboPatType2_1.SelectedIndex = 0
            cboPatType2_2.SelectedIndex = 0
        Catch ex As Exception

        End Try

    End Sub

#End Region
#Region "[멀티패턴단일]"
    Private Sub cbo멀티패턴단일설정_SelectedIndexChanged(sender As Object, e As EventArgs) Handles cbo멀티패턴단일설정.SelectedIndexChanged

        Try
            Dim _Money As cFareMultiPattern = New cFareMultiPattern(cur_member.reckey)

            lst멀티패턴단일설정.Items.Clear()

            Dim cur_money As List(Of cFareMultiPattern.HS_Pattern) = _Money.patternList.Where(Function(x) x.pattern_name = cbo멀티패턴단일설정.Text).ToList

            If cur_money.First.detail.Count > 0 Then
                cur_money.First.detail.ForEach(Sub(x)
                                                   Dim arr(6) As String
                                                   Dim itm As New ListViewItem

                                                   arr(0) = x.pattern_idx
                                                   arr(1) = ""
                                                   arr(2) = x.mesu
                                                   arr(3) = x.gametype
                                                   arr(4) = x.gametype2
                                                   arr(5) = x.pattern_name
                                                   arr(6) = x.pattern_reckey
                                                   itm = New ListViewItem(arr)
                                                   lst멀티패턴단일설정.Items.Add(itm)
                                               End Sub)
            End If

        Catch ex As Exception

        End Try

    End Sub

    Private Sub MaterialRaisedButton29_Click(sender As Object, e As EventArgs) Handles MaterialRaisedButton29.Click
        RestControlMultiPattern()
    End Sub

    Private Sub RestControlMultiPattern()
        lst멀티패턴단일설정.Items.Clear()
        cbo멀티패턴단일설정.Text = ""
    End Sub

    Private Sub MaterialRaisedButton28_Click(sender As Object, e As EventArgs) Handles MaterialRaisedButton28.Click

        Try
            If cbo멀티패턴단일설정.Text.Trim = "" Then Exit Sub
            cFareMultiPattern.setPatternSave(cur_member.reckey, cbo멀티패턴단일설정.Text.Trim, lst멀티패턴단일설정)
            Call RefreshMultiPatternCombo()
            Call RestControlMultiPattern()
            MsgBox("멀티패턴 저장 완료")
        Catch ex As Exception
        End Try

    End Sub

    Private Sub MaterialRaisedButton27_Click(sender As Object, e As EventArgs) Handles MaterialRaisedButton27.Click

        Try
            cFareMultiPattern.setPatternDelete(cur_member.reckey, cbo멀티패턴단일설정.Text.Trim)
            Call RefreshMultiPatternCombo()
            Call RestControlMultiPattern()
            MsgBox("멀티패턴 삭제 완료")
        Catch ex As Exception
        End Try
    End Sub

    Private Sub MaterialRaisedButton25_Click(sender As Object, e As EventArgs) Handles MaterialRaisedButton25.Click

        Dim arr(6) As String

        'Add items in the listview
        Dim itm As New ListViewItem

        'Add first item
        arr(0) = lst멀티패턴단일설정.Items.Count + 1
        arr(1) = ""
        arr(2) = txtMesu.Text
        arr(3) = cboBallType.Text
        arr(4) = cboGameType.Text
        Dim _Fare As cFarePattern = New cFarePattern(cur_member.reckey, cboPatType2_1.Text)
        arr(5) = _Fare.patternList.First.pattern_name
        arr(6) = _Fare.patternList.First.reckey
        itm = New ListViewItem(arr)
        lst멀티패턴단일설정.Items.Add(itm)
    End Sub

    Private Sub RefreshMultiPatternCombo()
        Try
            Dim _Money As cFareMultiPattern = New cFareMultiPattern(cur_member.reckey)

            cbo멀티패턴단일설정.Items.Clear()
            If cboBetType.SelectedIndex <> 1 Then
                cboPatType.Items.Clear()
            End If

            If _Money.patternList.Count > 0 Then
                _Money.patternList.ForEach(Sub(x)
                                               cbo멀티패턴단일설정.Items.Add(x.pattern_name)
                                               If cboBetType.SelectedIndex <> 1 Then
                                                   cboPatType.Items.Add(x.pattern_name)
                                               End If
                                           End Sub)
            End If
            If cboBetType.SelectedIndex <> 1 Then
                cboPatType.SelectedIndex = 0
            End If
        Catch ex As Exception
        End Try

    End Sub
    Private Sub MaterialRaisedButton9_Click(sender As Object, e As EventArgs) Handles MaterialRaisedButton9.Click
        Task.Run(Sub()

                     Dim sArr() As String = Split(txtMesu.Text, "-")

                     lst멀티패턴단일설정.BeginUpdate()

                     For i = CInt(sArr.First) To CInt(sArr.Last)
                         Dim arr(6) As String

                         'Add items in the listview
                         Dim itm As New ListViewItem

                         'Add first item
                         arr(0) = lst멀티패턴단일설정.Items.Count + 1
                         arr(1) = ""
                         arr(2) = i 'txtMesu.Text
                         arr(3) = cboBallType.Text
                         arr(4) = cboGameType.Text
                         Dim _Fare As cFarePattern = New cFarePattern(cur_member.reckey, cboPatType2_1.Text)
                         arr(5) = _Fare.patternList.First.pattern_name
                         arr(6) = _Fare.patternList.First.reckey
                         itm = New ListViewItem(arr)
                         lst멀티패턴단일설정.Items.Add(itm)
                     Next

                     lst멀티패턴단일설정.EndUpdate()
                 End Sub)
    End Sub
#End Region
#Region "[멀티패턴대중소]"
    Private Sub cbo멀티패턴대중소설정_SelectedIndexChanged(sender As Object, e As EventArgs) Handles cbo멀티패턴대중소설정.SelectedIndexChanged

        Try
            Dim _Money As cFareMultiPattern2 = New cFareMultiPattern2(cur_member.reckey)

            lst멀티패턴대중소설정.Items.Clear()

            Dim cur_money As List(Of cFareMultiPattern2.HS_Pattern) = _Money.patternList.Where(Function(x) x.pattern_name = cbo멀티패턴대중소설정.Text).ToList

            If cur_money.First.detail.Count > 0 Then
                cur_money.First.detail.ForEach(Sub(x)
                                                   Dim arr(6) As String
                                                   Dim itm As New ListViewItem

                                                   arr(0) = x.pattern_idx
                                                   arr(1) = ""
                                                   arr(2) = x.mesu
                                                   arr(3) = x.gametype
                                                   arr(4) = x.gametype2
                                                   arr(5) = x.pattern_name
                                                   arr(6) = x.pattern_reckey
                                                   itm = New ListViewItem(arr)
                                                   lst멀티패턴대중소설정.Items.Add(itm)
                                               End Sub)
            End If

        Catch ex As Exception

        End Try

    End Sub

    Private Sub MaterialRaisedButton8_Click(sender As Object, e As EventArgs) Handles MaterialRaisedButton8.Click
        RestControlMultiPattern2()
    End Sub

    Private Sub RestControlMultiPattern2()
        lst멀티패턴대중소설정.Items.Clear()
        cbo멀티패턴대중소설정.Text = ""
    End Sub

    Private Sub MaterialRaisedButton7_Click(sender As Object, e As EventArgs) Handles MaterialRaisedButton7.Click

        Try
            If cbo멀티패턴대중소설정.Text.Trim = "" Then Exit Sub
            cFareMultiPattern2.setPatternSave(cur_member.reckey, cbo멀티패턴대중소설정.Text.Trim, lst멀티패턴대중소설정)
            Call RefreshMultiPattern2Combo()
            Call RestControlMultiPattern2()
            MsgBox("멀티패턴 저장 완료")
        Catch ex As Exception
        End Try

    End Sub

    Private Sub MaterialRaisedButton6_Click(sender As Object, e As EventArgs) Handles MaterialRaisedButton6.Click

        Try
            cFareMultiPattern2.setPatternDelete(cur_member.reckey, cbo멀티패턴대중소설정.Text.Trim)
            Call RefreshMultiPattern2Combo()
            Call RestControlMultiPattern2()
            MsgBox("멀티패턴 삭제 완료")
        Catch ex As Exception
        End Try
    End Sub

    Private Sub MaterialRaisedButton5_Click(sender As Object, e As EventArgs) Handles MaterialRaisedButton5.Click

        Dim arr(6) As String

        'Add items in the listview
        Dim itm As New ListViewItem

        'Add first item
        arr(0) = lst멀티패턴대중소설정.Items.Count + 1
        arr(1) = ""
        arr(2) = txtMesu2.Text
        arr(3) = cbo대중소.Text
        arr(4) = IIf(chk역방향.Checked, "역방향", "정방향")
        Dim _Fare As cFarePattern = New cFarePattern(cur_member.reckey, cboPatType2_2.Text)
        arr(5) = _Fare.patternList.First.pattern_name
        arr(6) = _Fare.patternList.First.reckey
        itm = New ListViewItem(arr)
        lst멀티패턴대중소설정.Items.Add(itm)
    End Sub

    Private Sub RefreshMultiPattern2Combo()
        Try
            Dim _Money As cFareMultiPattern2 = New cFareMultiPattern2(cur_member.reckey)

            cbo멀티패턴대중소설정.Items.Clear()
            If cboBetType.SelectedIndex = 1 Then
                cboPatType.Items.Clear()
            End If

            If _Money.patternList.Count > 0 Then
                _Money.patternList.ForEach(Sub(x)
                                               cbo멀티패턴대중소설정.Items.Add(x.pattern_name)
                                               If cboBetType.SelectedIndex = 1 Then
                                                   cboPatType.Items.Add(x.pattern_name)
                                               End If
                                           End Sub)
            End If
            If cboBetType.SelectedIndex = 1 Then
                cboPatType.SelectedIndex = 0
            End If
        Catch ex As Exception
        End Try

    End Sub
#End Region

    Private Sub cboBetType_SelectedIndexChanged(sender As Object, e As EventArgs) Handles cboBetType.SelectedIndexChanged

        If cboBetType.Text = "멀티패턴(대중소)" Then
            RefreshMultiPattern2Combo()
        Else
            RefreshMultiPatternCombo()
        End If

    End Sub

    Private Sub Button7_Click(sender As Object, e As EventArgs) Handles Button7.Click

        If Button7.Text = "배팅시작" Then
            Button7.Text = "배팅중지"
            ClearVirtual패턴설정()

            Dim s패턴 As String = ""
            Dim s단계 As String = "1-1"
            Dim s승무패 As String = "0/0/0"

            _all_cFarePattern = New cFarePattern(cur_member.reckey)
            _SumList = New List(Of _Sum)

            lst패턴설정.BeginUpdate()

            Dim lstview As New List(Of ListViewItem)

            Select Case cboBetType.Text
                Case "멀티패턴(단일)"
                    _cMultiPattern = New cFareMultiPattern(cur_member.reckey, cboPatType.Text)
                    For j = 0 To _cMultiPattern.patternList.First.detail.Count - 1
                        s패턴 = _cMultiPattern.patternList.First.detail(j).pattern_name
                        lstview.Add(AddPatList(cboBetType.SelectedIndex, s패턴, s단계, s승무패, _cMultiPattern.patternList.First.detail(j).gametype, _cMultiPattern.patternList.First.detail(j).gametype2,
                               _cMultiPattern.patternList.First.detail(j).pattern_reckey, _cMultiPattern.patternList.First.detail(j).mesu))
                        Dim jj As Integer = j
                        Dim __Sumlist = _SumList.Where(Function(x) x._Mesu = _cMultiPattern.patternList.First.detail(jj).mesu And
                                                       x._GameType = _cMultiPattern.patternList.First.detail(jj).gametype And
                                                       x._GameType2 = _cMultiPattern.patternList.First.detail(jj).gametype2).ToList
                        If __Sumlist.Count = 0 Then
                            _SumList.Add(New _Sum(_cMultiPattern.patternList.First.detail(j).mesu, cboBetType.SelectedIndex, _cMultiPattern.patternList.First.detail(j).gametype, _cMultiPattern.patternList.First.detail(j).gametype2))
                        End If
                    Next
                Case "멀티패턴(조합배팅)", "멀티패턴(조합배팅서식)"
                    _cMultiPattern = New cFareMultiPattern(cur_member.reckey, cboPatType.Text)
                    For j = 0 To _cMultiPattern.patternList.First.detail.Count - 1
                        s패턴 = _cMultiPattern.patternList.First.detail(j).pattern_name
                        lstview.Add(AddPatList(cboBetType.SelectedIndex, s패턴, s단계, s승무패, "", "",
                               _cMultiPattern.patternList.First.detail(j).pattern_reckey, _cMultiPattern.patternList.First.detail(j).mesu))
                        Dim jj As Integer = j
                        Dim __Sumlist = _SumList.Where(Function(x) x._Mesu = _cMultiPattern.patternList.First.detail(jj).mesu And
                                                       x._GameType = _cMultiPattern.patternList.First.detail(jj).gametype And
                                                       x._GameType2 = _cMultiPattern.patternList.First.detail(jj).gametype2).ToList
                        If __Sumlist.Count = 0 Then
                            _SumList.Add(New _Sum(_cMultiPattern.patternList.First.detail(j).mesu, cboBetType.SelectedIndex, _cMultiPattern.patternList.First.detail(j).gametype, _cMultiPattern.patternList.First.detail(j).gametype2))
                        End If
                    Next
                Case "멀티패턴(대중소)"
                    _cMultiPattern2 = New cFareMultiPattern2(cur_member.reckey, cboPatType.Text)
                    For j = 0 To _cMultiPattern2.patternList.First.detail.Count - 1
                        s패턴 = _cMultiPattern2.patternList.First.detail(j).pattern_name
                        lstview.Add(AddPatList(cboBetType.SelectedIndex, s패턴, s단계, s승무패, _cMultiPattern2.patternList.First.detail(j).gametype, _cMultiPattern2.patternList.First.detail(j).gametype2,
                               _cMultiPattern2.patternList.First.detail(j).pattern_reckey, _cMultiPattern2.patternList.First.detail(j).mesu))
                        Dim jj As Integer = j
                        Dim __Sumlist = _SumList.Where(Function(x) x._Mesu = _cMultiPattern2.patternList.First.detail(jj).mesu And
                                                       x._GameType = _cMultiPattern2.patternList.First.detail(jj).gametype And
                                                       x._GameType2 = _cMultiPattern2.patternList.First.detail(jj).gametype2).ToList
                        If __Sumlist.Count = 0 Then
                            _SumList.Add(New _Sum(_cMultiPattern2.patternList.First.detail(j).mesu, cboBetType.SelectedIndex, _cMultiPattern2.patternList.First.detail(j).gametype, _cMultiPattern2.patternList.First.detail(j).gametype2))
                        End If
                    Next
            End Select

            For Each item As ListViewItem In lstview
                AddVirtual패턴설정Item(item.SubItems.Cast(Of ListViewItem.ListViewSubItem)().Select(Function(si) si.Text).ToArray())
            Next

            lstview = New List(Of ListViewItem)
            lst패턴설정.EndUpdate()

            ClearVirtual단계별통계()

            Dim iCnt As Integer = 0
            _SumList.ForEach(Sub(X)
                                 X._RowCnt = iCnt
                                 iCnt += 1

                                 Dim arr(6) As String
                                 Dim itm As New ListViewItem

                                 arr(0) = ""
                                 arr(1) = X._Mesu
                                 arr(2) = ""
                                 arr(3) = ""
                                 arr(4) = ""
                                 arr(5) = "1-1"
                                 arr(6) = "0"

                                 Select Case X._BetType
                                     Case 0
                                         arr(2) = X._GameType
                                         arr(3) = X._GameType2
                                     Case 1
                                         arr(3) = X._GameType2
                                         arr(4) = X._GameType
                                 End Select

                                 AddVirtual단계별통계Item(arr)
                             End Sub)

            mesu_list_run = New List(Of mesu_class)

            _SumList.ForEach(Sub(x)
                                 If mesu_list_run.Where(Function(xx) xx.mesu = x._Mesu).ToList.Count = 0 Then
                                     Dim iiii As Integer = x._Mesu
                                     Dim mesu_list_c As mesu_class
                                     Dim mesu_list As New List(Of Integer)
                                     Dim jj As Integer = 1
                                     Dim iCnt2 As Integer = 0

                                     For i = 0 To iiii - 1
                                         mesu_list = New List(Of Integer)
                                         Dim iVal As Integer = i + 1
                                         mesu_list.Add(iVal)

                                         Do While iVal < 3000
                                             iVal += iiii
                                             mesu_list.Add(iVal)
                                         Loop
                                         mesu_list_c = New mesu_class(x._Mesu, iCnt2, mesu_list) ' New mesu_class(ii, mesu_list_all.Count, mesu_list)
                                         iCnt2 += 1
                                         mesu_list_run.Add(mesu_list_c)
                                     Next
                                 End If
                             End Sub)

            ResetAmount()
        Else
            Button7.Text = "배팅시작"
        End If

    End Sub

    Public Sub Set_Open()

        _BetList = New List(Of _Bet)

        cLogManager.Instance.AddToLogRoom("", String.Format("[{0}] Set_Open 수신", Now.ToString("HH:mm")))

        If Button7.Text = "배팅시작" Then
            Exit Sub
        End If

        Dim cur_money As Long = CLng(txt현재금액.Text)

        Select Case cboBetType.SelectedIndex
            Case 0
                Dim aStr1 As String = String.Join("", _GameList.Select(Function(history) history.gUnderOver))
                Dim aStr2 As String = String.Join("", _GameList.Select(Function(history) history.gOddEven))
                Dim aStr3 As String = String.Join("", _GameList.Select(Function(history) history.pUnderOver))
                Dim aStr4 As String = String.Join("", _GameList.Select(Function(history) history.pOddEven))
                Dim __len As Integer = _GameList.Count

                Dim betBag As New ConcurrentBag(Of _Bet)()

                For iii = 0 To GetVirtual패턴설정Count() - 1
                    Dim itemData = GetVirtual패턴설정Item(iii)
                    
                    Dim _mesuStr As String = itemData(2)
                    Dim mesu As Integer
                    If Not Integer.TryParse(_mesuStr, mesu) Then Return

                    Dim mesu_cur = mesu_list_run.Where(Function(x) x.mesu = mesu).ToList()
                    If mesu_cur.Count = 0 Then Return

                    Dim gametype As String = itemData(8)
                    Dim gametype2 As String = itemData(9)
                    Dim _Level As String = itemData(3)
                    Dim pat_key As String = itemData(10)

                    Dim _Cur = _all_cFarePattern.patternList.FirstOrDefault(Function(x) x.reckey = pat_key)
                    If _Cur Is Nothing Then Return

                    Dim _ResultStr As String = ""
                    Select Case gametype
                        Case "일반볼"
                            _ResultStr = If(gametype2 = "언오버", aStr1, aStr2)
                        Case "파워볼"
                            _ResultStr = If(gametype2 = "언오버", aStr3, aStr4)
                    End Select

                    Dim neededLength As Integer = mesu * 2 + (_ResultStr.Length Mod mesu)
                    Dim partialStr As String = If(neededLength <= _ResultStr.Length, _ResultStr.Substring(_ResultStr.Length - neededLength), _ResultStr)

                    Dim myList = CreateGroupedListFromText(partialStr, mesu)

                    Dim jj As Integer = mesu_cur.Where(Function(x) x.list.Where(Function(xx) xx = partialStr.Length).Count() = 1).First().cnt
                    Dim ii As Integer = mesu_cur.Where(Function(x) x.list.Where(Function(xx) xx = partialStr.Length).Count() = 1).First().list.IndexOf(partialStr.Length)

                    If ii > 0 AndAlso jj + 1 <> myList.Count Then
                        Dim _jj = mesu_cur.Where(Function(x) x.list.Where(Function(xx) xx = partialStr.Length + 1).Count() = 1).First().cnt
                        Dim _ii = mesu_cur.Where(Function(x) x.list.Where(Function(xx) xx = partialStr.Length + 1).Count() = 1).First().list.IndexOf(partialStr.Length + 1)

                        Dim _Prev_mesu_line As Char = myList(_jj).CharList(_ii - 1)
                        Dim _Prev_mesu As Char = myList(jj).CharList(ii - 1)
                        Dim _Prev2_mesu As Char = _ResultStr.Last()

                        Dim _pat As List(Of HS_Game_Pattern) = chkPattern(_Cur.pattern_value)
                        Dim aAmount As Long = GetAmount(_pat, _Level)

                        Dim bStr2 As String
                        If _Prev_mesu = _Prev2_mesu Then
                            bStr2 = _Prev_mesu_line
                        Else
                            bStr2 = If(gametype2 = "언오버", If(_Prev_mesu_line = "U"c, "O", "U"), If(_Prev_mesu_line = "O"c, "E", "O"))
                        End If

                        Dim newBet As New _Bet(aAmount, bStr2, cboBetType.SelectedIndex, gametype, gametype2, _mesuStr, iii, _Level)
                        betBag.Add(newBet)
                    End If
                Next

                'Parallel.For(0, lst패턴설정.Items.Count,
                'Sub(iii)
                'End Sub)

                ' 병렬 처리 후 List로 변환
                _BetList = betBag.ToList()
                _BetList = _BetList.OrderBy(Function(x) x._RowCnt).ToList

                Dim bSum As New List(Of _Bet)
                If _BetList.Count > 0 Then
                    _BetList.ForEach(Sub(x)
                                         Dim aaa = bSum.Where(Function(xx) xx._GameType = x._GameType And xx._GameType2 = x._GameType2 And xx._BetVal = x._BetVal).ToList

                                         If aaa.Count = 0 Then
                                             bSum.Add(New _Bet(x._Amount, x._BetVal, x._BetType, x._GameType, x._GameType2, 0, 0, ""))
                                         Else
                                             aaa.First._Amount = aaa.First._Amount + x._Amount
                                         End If
                                     End Sub)

                End If

                Dim tAmount As Long = bSum.Sum(Function(x) x._Amount)

                If tAmount <= cur_money Then
                    SetTotalAmount(tAmount)

                    Invoke(Sub()
                               'Dim itemsToAdd As New List(Of String)()
                               For Each bet In _BetList
                                   SetVirtual패턴설정SubItem(bet._RowCnt, 5, bet._BetVal)
                                   SetVirtual패턴설정SubItem(bet._RowCnt, 7, bet._Amount)

                                   Dim itemData = GetVirtual패턴설정Item(bet._RowCnt)
                                   cLogManager.Instance.AddToLogBet($"[{Now:HH:mm}]{itemData(1)} 매수:{itemData(2)} {bet._BetVal} {bet._GameType} {bet._GameType2} {bet._Level}단계 {bet._Amount}원 배팅")
                                   'itemsToAdd.Add($"[{Now:HH:mm}]{lst패턴설정.Items(bet._RowCnt).SubItems(1).Text} 매수:{lst패턴설정.Items(bet._RowCnt).SubItems(2).Text} {bet._BetVal} {bet._GameType} {bet._GameType2} {bet._Level}단계 {bet._Amount}원 배팅")
                                   'SetListbox($"[{Now:HH:mm}]{lst패턴설정.Items(bet._RowCnt).SubItems(1).Text} 매수:{lst패턴설정.Items(bet._RowCnt).SubItems(2).Text} {bet._BetVal} {bet._GameType} {bet._GameType2} {bet._Level}단계 {bet._Amount}원 배팅")
                               Next
                               'SetListboxRange(itemsToAdd.ToArray())

                               'itemsToAdd = New List(Of String)()
                           End Sub)


                    Dim bSum2 As New List(Of _Bet)
                    bSum.ForEach(Async Sub(x)
                                     bSum2.Add(x)
                                     If rd가상.Checked = False Then
                                         'ListBox2.Items.Add(p_changeBetAmount(x, _cur_game.args.gameId, "1"))
                                         Await SendWebSocketMessageAsync("powerball/player/game", p_changeBetAmount(x, _cur_game.args.gameId, "1"))
                                         Await SendWebSocketMessageAsync("powerball/player/game", p_Bet(x, bSum2, _cur_game.args.number, _cur_game.args.gameId))
                                     End If
                                     SetListbox(String.Format("[{4}] {3} : {0}-{1}-{5}, {2}원 배팅", x._GameType, x._GameType2, x._Amount, cboBetType.Text, Now.ToString("HH:mm"), x._BetVal))
                                 End Sub)

                    UpdateMaxLevel(_BetList)
                Else
                    SetListbox(String.Format("[{0}] 금액 부족으로 인한 배팅 미실행", Now.ToString("HH:mm")))

                    For iii = 0 To GetVirtual패턴설정Count() - 1
                        SetVirtual패턴설정SubItem(iii, 4, "")
                    Next
                    _BetList = New List(Of _Bet)
                End If
            Case 1
                'For iii = 0 To lst패턴설정.Items.Count - 1
                '    Dim _mesu As String = lst패턴설정.Items(iii).SubItems(9).Text
                '    Dim __len As Integer = 300 '_GameList.Count
                '    Dim mesu_cur = mesu_list_run.Where(Function(x) x.mesu = _mesu).ToList

                '    Dim jj As Integer = mesu_cur.Where(Function(x) x.list.Where(Function(xx) xx = __len + 1).ToList.Count = 1).ToList.First.cnt
                '    Dim ii As Integer = mesu_cur.Where(Function(x) x.list.Where(Function(xx) xx = __len + 1).ToList.Count = 1).ToList.First.list.IndexOf(__len + 1)

                '    If jj = 0 Then
                '        lst패턴설정.Items(iii).SubItems(10).Text = 1
                '    End If

                '    Dim 진행여부 As String = lst패턴설정.Items(iii).SubItems(10).Text
                '    Dim pat_key As String = lst패턴설정.Items(iii).SubItems(8).Text
                '    Dim _Cur = _all_cFarePattern.patternList.Where(Function(x) x.reckey = pat_key).ToList

                '    If _Cur.Count > 0 AndAlso 진행여부 <> "0" Then
                '        Dim _Level As String = lst패턴설정.Items(iii).SubItems(2).Text
                '        Dim _pat As List(Of HS_Game_Pattern) = chkPattern(_Cur.First.pattern_value)

                '        If _pat.Count > 0 Then
                '            Dim aAmount As Long = GetAmount(_pat, _Level)
                '            Dim gametype As String = lst패턴설정.Items(iii).SubItems(6).Text
                '            Dim gametype2 As String = lst패턴설정.Items(iii).SubItems(7).Text

                '            If gametype2 = "정방향" Then
                '                lst패턴설정.Items(iii).SubItems(4).Text = gametype
                '                _BetList.Add(New _Bet(aAmount, gametype, cboBetType.SelectedIndex, gametype, gametype2, _mesu, iii, _Level))
                '            Else
                '                Select Case gametype
                '                    Case "대"
                '                        lst패턴설정.Items(iii).SubItems(4).Text = "중"
                '                        _BetList.Add(New _Bet(aAmount, "중", cboBetType.SelectedIndex, gametype, gametype2, _mesu, iii, _Level))

                '                        lst패턴설정.Items(iii).SubItems(4).Text = "소"
                '                        _BetList.Add(New _Bet(aAmount, "소", cboBetType.SelectedIndex, gametype, gametype2, _mesu, iii, _Level))
                '                    Case "중"
                '                        lst패턴설정.Items(iii).SubItems(4).Text = "대"
                '                        _BetList.Add(New _Bet(aAmount, "대", cboBetType.SelectedIndex, gametype, gametype2, _mesu, iii, _Level))

                '                        lst패턴설정.Items(iii).SubItems(4).Text = "소"
                '                        _BetList.Add(New _Bet(aAmount, "소", cboBetType.SelectedIndex, gametype, gametype2, _mesu, iii, _Level))
                '                    Case "소"
                '                        lst패턴설정.Items(iii).SubItems(4).Text = "대"
                '                        _BetList.Add(New _Bet(aAmount, "대", cboBetType.SelectedIndex, gametype, gametype2, _mesu, iii, _Level))

                '                        lst패턴설정.Items(iii).SubItems(4).Text = "중"
                '                        _BetList.Add(New _Bet(aAmount, "중", cboBetType.SelectedIndex, gametype, gametype2, _mesu, iii, _Level))
                '                End Select
                '            End If
                '        End If
                '    End If
                'Next

                'Dim bSum As New List(Of _Bet)
                'If _BetList.Count > 0 Then
                '    _BetList.ForEach(Sub(x)
                '                         Dim aaa = bSum.Where(Function(xx) xx._BetVal = x._BetVal).ToList

                '                         If aaa.Count = 0 Then
                '                             bSum.Add(New _Bet(x._Amount, x._BetVal, x._BetType, x._GameType, x._GameType2, 0, 0, ""))
                '                         Else
                '                             aaa.First._Amount = aaa.First._Amount + x._Amount
                '                         End If
                '                     End Sub)
                'End If

                'Dim tAmount As Long = bSum.Sum(Function(x) x._Amount)

                'If tAmount <= cur_money Then
                '    SetTotalAmount(tAmount)

                '    Dim bSum2 As New List(Of _Bet)
                '    bSum.ForEach(Sub(x)
                '                     bSum2.Add(x)
                '                     If rd가상.Checked = False Then
                '                         WS_Send_Sync(p_Bet2(x, bSum2, _cur_game.args.number, _cur_game.args.gameId))
                '                         System.Threading.Thread.Sleep(100)
                '                     End If
                '                     UpdateListbox(ListBox1, String.Format("[{4}] {3} : {0}-{1}, {2}원 배팅", "대중소", x._BetVal, x._Amount, cboBetType.Text, Now.ToString("HH:mm")))
                '                 End Sub)

                '    _BetList.ForEach(Sub(x)
                '                         UpdateMaxLevel(x)
                '                     End Sub)
                'Else
                '    UpdateListbox(ListBox1, String.Format("[{0}] 금액 부족으로 인한 배팅 미실행", Now.ToString("HH:mm")))

                '    For iii = 0 To lst패턴설정.Items.Count - 1
                '        lst패턴설정.Items(iii).SubItems(4).Text = ""
                '    Next
                '    _BetList = New List(Of _Bet)
                'End If
            Case 2
                Dim aStr1 As String = String.Join("", _GameList.Select(Function(history) history.gUnderOver))
                Dim aStr2 As String = String.Join("", _GameList.Select(Function(history) history.gOddEven))
                Dim __len As Integer = _GameList.Count

                Dim betBag As New ConcurrentBag(Of _Bet)()

                For iii = 0 To lst패턴설정.Items.Count - 1
                    Dim itemData = GetVirtual패턴설정Item(iii)
                    Dim _mesuStr As String = itemData(2)
                    Dim mesu As Integer
                    If Not Integer.TryParse(_mesuStr, mesu) Then Return

                    Dim mesu_cur = mesu_list_run.Where(Function(x) x.mesu = mesu).ToList()
                    If mesu_cur.Count = 0 Then Return

                    Dim _Level As String = itemData(3)
                    Dim pat_key As String = itemData(10)

                    Dim _Cur = _all_cFarePattern.patternList.FirstOrDefault(Function(x) x.reckey = pat_key)
                    If _Cur Is Nothing Then Return

                    Dim _ResultStr As String = aStr1
                    Dim _ResultStr2 As String = aStr2

                    Dim neededLength As Integer = mesu * 2 + (_ResultStr.Length Mod mesu)
                    Dim partialStr As String = If(neededLength <= _ResultStr.Length, _ResultStr.Substring(_ResultStr.Length - neededLength), _ResultStr)
                    Dim partialStr2 As String = If(neededLength <= _ResultStr2.Length, _ResultStr2.Substring(_ResultStr2.Length - neededLength), _ResultStr2)

                    Dim myList = CreateGroupedListFromText(partialStr, mesu)
                    Dim myList2 = CreateGroupedListFromText(partialStr2, mesu)

                    Dim jj As Integer = mesu_cur.Where(Function(x) x.list.Where(Function(xx) xx = partialStr.Length).Count() = 1).First().cnt
                    Dim ii As Integer = mesu_cur.Where(Function(x) x.list.Where(Function(xx) xx = partialStr.Length).Count() = 1).First().list.IndexOf(partialStr.Length)

                    If ii > 0 Then
                        Dim _jj = mesu_cur.Where(Function(x) x.list.Where(Function(xx) xx = partialStr.Length + 1).Count() = 1).First().cnt
                        Dim _ii = mesu_cur.Where(Function(x) x.list.Where(Function(xx) xx = partialStr.Length + 1).Count() = 1).First().list.IndexOf(partialStr.Length + 1)

                        Dim _Prev_mesu_line As Char = myList(_jj).CharList(_ii - 1)
                        Dim _Prev_mesu_line2 As Char = myList2(_jj).CharList(_ii - 1)

                        Dim _pat As List(Of HS_Game_Pattern) = chkPattern(_Cur.pattern_value)
                        Dim aAmount As Long = GetAmount(_pat, _Level)

                        Dim bStr2 As String = $"{_Prev_mesu_line}{_Prev_mesu_line2}"

                        Dim newBet As New _Bet(aAmount, bStr2, cboBetType.SelectedIndex, "", "", _mesuStr, iii, _Level)
                        betBag.Add(newBet)
                    End If
                Next

                ' 병렬 처리 후 List로 변환
                _BetList = betBag.ToList()
                _BetList = _BetList.OrderBy(Function(x) x._RowCnt).ToList

                Dim bSum As New List(Of _Bet)
                If _BetList.Count > 0 Then
                    _BetList.ForEach(Sub(x)
                                         Dim aaa = bSum.Where(Function(xx) xx._BetVal = x._BetVal).ToList

                                         If aaa.Count = 0 Then
                                             bSum.Add(New _Bet(x._Amount, x._BetVal, x._BetType, x._GameType, x._GameType2, 0, 0, ""))
                                         Else
                                             aaa.First._Amount = aaa.First._Amount + x._Amount
                                         End If
                                     End Sub)

                End If

                Dim tAmount As Long = bSum.Sum(Function(x) x._Amount)

                If tAmount <= cur_money Then
                    SetTotalAmount(tAmount)

                    Invoke(Sub()
                               'Dim itemsToAdd As New List(Of String)()
                               For Each bet In _BetList
                                   SetVirtual패턴설정SubItem(bet._RowCnt, 5, bet._BetVal)
                                   SetVirtual패턴설정SubItem(bet._RowCnt, 7, bet._Amount)

                                   Dim itemData = GetVirtual패턴설정Item(bet._RowCnt)
                                   cLogManager.Instance.AddToLogBet($"[{Now:HH:mm}]{itemData(1)} 매수:{itemData(2)} {bet._BetVal} {bet._GameType} {bet._GameType2} {bet._Level}단계 {bet._Amount}원 배팅")
                                   'itemsToAdd.Add($"[{Now:HH:mm}]{lst패턴설정.Items(bet._RowCnt).SubItems(1).Text} 매수:{lst패턴설정.Items(bet._RowCnt).SubItems(2).Text} {bet._BetVal} {bet._GameType} {bet._GameType2} {bet._Level}단계 {bet._Amount}원 배팅")
                                   'SetListbox($"[{Now:HH:mm}]{lst패턴설정.Items(bet._RowCnt).SubItems(1).Text} 매수:{lst패턴설정.Items(bet._RowCnt).SubItems(2).Text} {bet._BetVal} {bet._GameType} {bet._GameType2} {bet._Level}단계 {bet._Amount}원 배팅")
                               Next
                               'SetListboxRange(itemsToAdd.ToArray())

                               'itemsToAdd = New List(Of String)()
                           End Sub)


                    Dim bSum2 As New List(Of _Bet)
                    bSum.ForEach(Async Sub(x)
                                     bSum2.Add(x)
                                     If rd가상.Checked = False Then
                                         'ListBox2.Items.Add(p_changeBetAmount(x, _cur_game.args.gameId, "1"))
                                         Await SendWebSocketMessageAsync("powerball/player/game", p_changeBetAmount(x, _cur_game.args.gameId, "1"))
                                         Await SendWebSocketMessageAsync("powerball/player/game", p_Bet(x, bSum2, _cur_game.args.number, _cur_game.args.gameId))
                                     End If
                                     SetListbox(String.Format("[{4}] {3} : {0}-{1}-{5}, {2}원 배팅", x._GameType, x._GameType2, x._Amount, cboBetType.Text, Now.ToString("HH:mm"), x._BetVal))
                                 End Sub)

                    UpdateMaxLevel(_BetList)
                Else
                    SetListbox(String.Format("[{0}] 금액 부족으로 인한 배팅 미실행", Now.ToString("HH:mm")))

                    For iii = 0 To GetVirtual패턴설정Count() - 1
                        SetVirtual패턴설정SubItem(iii, 4, "")
                    Next
                    _BetList = New List(Of _Bet)
                End If
            Case 3
                Dim aStr1 As String = String.Join("", _GameList.Select(Function(history) history.gUnderOver))
                Dim aStr2 As String = String.Join("", _GameList.Select(Function(history) history.gOddEven))
                Dim __len As Integer = _GameList.Count

                Dim betBag As New ConcurrentBag(Of _Bet)()

                For iii = 0 To lst패턴설정.Items.Count - 1
                    System.Threading.Thread.Sleep(100)
                    Dim itemData = GetVirtual패턴설정Item(iii)
                    Dim _mesuStr As String = itemData(2)
                    Dim mesu As Integer
                    If Not Integer.TryParse(_mesuStr, mesu) Then Return

                    Dim mesu_cur = mesu_list_run.Where(Function(x) x.mesu = mesu).ToList()
                    If mesu_cur.Count = 0 Then Return

                    Dim _Level As String = itemData(3)
                    Dim pat_key As String = itemData(10)

                    Dim _Cur = _all_cFarePattern.patternList.FirstOrDefault(Function(x) x.reckey = pat_key)
                    If _Cur Is Nothing Then Return

                    Dim _ResultStr As String = aStr1
                    Dim _ResultStr2 As String = aStr2

                    Dim neededLength As Integer = mesu * 2 + (_ResultStr.Length Mod mesu)
                    Dim partialStr As String = If(neededLength <= _ResultStr.Length, _ResultStr.Substring(_ResultStr.Length - neededLength), _ResultStr)
                    Dim partialStr2 As String = If(neededLength <= _ResultStr2.Length, _ResultStr2.Substring(_ResultStr2.Length - neededLength), _ResultStr2)

                    Dim myList = CreateGroupedListFromText(partialStr, mesu)
                    Dim myList2 = CreateGroupedListFromText(partialStr2, mesu)

                    Dim jj As Integer = mesu_cur.Where(Function(x) x.list.Where(Function(xx) xx = partialStr.Length).Count() = 1).First().cnt
                    Dim ii As Integer = mesu_cur.Where(Function(x) x.list.Where(Function(xx) xx = partialStr.Length).Count() = 1).First().list.IndexOf(partialStr.Length)

                    If ii > 0 Then
                        Dim _jj = mesu_cur.Where(Function(x) x.list.Where(Function(xx) xx = partialStr.Length + 1).Count() = 1).First().cnt
                        Dim _ii = mesu_cur.Where(Function(x) x.list.Where(Function(xx) xx = partialStr.Length + 1).Count() = 1).First().list.IndexOf(partialStr.Length + 1)

                        Dim _Prev_mesu_line As Char = myList(_jj).CharList(_ii - 1)
                        Dim _Prev_mesu_line2 As Char = myList2(_jj).CharList(_ii - 1)

                        Dim _pat As List(Of HS_Game_Line_Data) = chkPattern_Multi(_Cur.pattern_value)
                        Dim aAmount As Long = GetAmount_Multi(_pat, _Level)

                        Dim lineval As String = $"{IIf(_Prev_mesu_line = "U", "1", "2")}-{IIf(_Prev_mesu_line2 = "O", "1", "2")}"

                        Dim aLevel() As String = Split(_Level, "-")

                        If _pat.Count > 0 Then
                            For ix = 0 To _pat(aLevel.First - 1).GamePatterns(aLevel.Last - 1).MoneyPatterns.Count - 1
                                If _pat(aLevel.First - 1).GamePatterns(aLevel.Last - 1).MoneyPatterns(ix).Key = lineval Then
                                    If _pat(aLevel.First - 1).GamePatterns(aLevel.Last - 1).MoneyPatterns(ix).Patterns.Count > 0 Then
                                        For jx = 0 To _pat(aLevel.First - 1).GamePatterns(aLevel.Last - 1).MoneyPatterns(ix).Patterns.Count - 1
                                            Dim jxs As String = IIf(_pat(aLevel.First - 1).GamePatterns(aLevel.Last - 1).MoneyPatterns(ix).Patterns(jx).LastPart = "1", "U", "O")
                                            Dim jxs2 As String = IIf(_pat(aLevel.First - 1).GamePatterns(aLevel.Last - 1).MoneyPatterns(ix).Patterns(jx).FirstPart = "1", "O", "E")
                                            Dim bStr2 As String = $"{jxs}{jxs2}"
                                            Dim newBet As New _Bet(aAmount, bStr2, cboBetType.SelectedIndex, "", "", _mesuStr, iii, _Level)
                                            betBag.Add(newBet)
                                        Next
                                        Exit For
                                    End If
                                End If
                            Next
                        End If

                    End If
                Next

                ' 병렬 처리 후 List로 변환
                _BetList = betBag.ToList()
                _BetList = _BetList.OrderBy(Function(x) x._RowCnt).ToList

                Dim bSum As New List(Of _Bet)
                If _BetList.Count > 0 Then
                    _BetList.ForEach(Sub(x)
                                         Dim aaa = bSum.Where(Function(xx) xx._BetVal = x._BetVal).ToList

                                         If aaa.Count = 0 Then
                                             bSum.Add(New _Bet(x._Amount, x._BetVal, x._BetType, x._GameType, x._GameType2, 0, 0, ""))
                                         Else
                                             aaa.First._Amount = aaa.First._Amount + x._Amount
                                         End If
                                     End Sub)

                End If

                Dim tAmount As Long = bSum.Sum(Function(x) x._Amount)

                If tAmount <= cur_money Then
                    SetTotalAmount(tAmount)

                    Invoke(Sub()
                               'Dim itemsToAdd As New List(Of String)()
                               For Each bet In _BetList
                                   Dim betItemData = GetVirtual패턴설정Item(bet._RowCnt)
                                   cLogManager.Instance.AddToLogBet($"[{Now:HH:mm}]{betItemData(1)} 매수:{betItemData(2)} {bet._BetVal} {bet._GameType} {bet._GameType2} {bet._Level}단계 {bet._Amount}원 배팅")
                                   'itemsToAdd.Add($"[{Now:HH:mm}]{lst패턴설정.Items(bet._RowCnt).SubItems(1).Text} 매수:{lst패턴설정.Items(bet._RowCnt).SubItems(2).Text} {bet._BetVal} {bet._GameType} {bet._GameType2} {bet._Level}단계 {bet._Amount}원 배팅")
                                   'SetListbox($"[{Now:HH:mm}]{lst패턴설정.Items(bet._RowCnt).SubItems(1).Text} 매수:{lst패턴설정.Items(bet._RowCnt).SubItems(2).Text} {bet._BetVal} {bet._GameType} {bet._GameType2} {bet._Level}단계 {bet._Amount}원 배팅")
                               Next
                               'SetListboxRange(itemsToAdd.ToArray())

                               For iii = 0 To GetVirtual패턴설정Count() - 1
                                   Dim _BetAmount As Long = _BetList.Where(Function(x) x._RowCnt = iii).Sum(Function(x) x._Amount)
                                   SetVirtual패턴설정SubItem(iii, 7, _BetAmount.ToString())
                               Next
                               'itemsToAdd = New List(Of String)()
                           End Sub)


                    Dim bSum2 As New List(Of _Bet)
                    bSum.ForEach(Async Sub(x)
                                     bSum2.Add(x)
                                     If rd가상.Checked = False Then
                                         'ListBox2.Items.Add(p_changeBetAmount(x, _cur_game.args.gameId, "1"))
                                         Await SendWebSocketMessageAsync("powerball/player/game", p_changeBetAmount(x, _cur_game.args.gameId, "1"))
                                         Await SendWebSocketMessageAsync("powerball/player/game", p_Bet(x, bSum2, _cur_game.args.number, _cur_game.args.gameId))
                                     End If
                                     SetListbox(String.Format("[{4}] {3} : {0}-{1}-{5}, {2}원 배팅", x._GameType, x._GameType2, x._Amount, cboBetType.Text, Now.ToString("HH:mm"), changetype3(x._BetVal)))
                                 End Sub)

                    UpdateMaxLevel(_BetList)
                Else
                    SetListbox(String.Format("[{0}] 금액 부족으로 인한 배팅 미실행", Now.ToString("HH:mm")))

                    For iii = 0 To GetVirtual패턴설정Count() - 1
                        SetVirtual패턴설정SubItem(iii, 4, "")
                    Next
                    _BetList = New List(Of _Bet)
                End If
        End Select

    End Sub

    Private Function changetype3(str As String) As String

        Dim ret As String = ""

        Select Case str
            Case "OO"
                ret = "홀오버"
            Case "OE"
                ret = "짝오버"
            Case "UO"
                ret = "홀언더"
            Case "UE"
                ret = "짝언더"
        End Select

        Return ret

    End Function

    Public Sub Set_Close(lstCnt As Integer)

        If _BetList.Count = 0 Then
            Exit Sub
        End If

        Dim b가상Checked As Boolean = rd가상.Checked
        Dim lResult As Long = 0
        Dim _il As Integer = 0
        Dim _id As Integer = 0
        Dim _iw As Integer = 0
        Dim type3str As String = ""

        _BetList.ForEach(Sub(x)
                             Select Case x._BetType
                                 Case 0
                                     Select Case x._GameType
                                         Case "일반볼"
                                             Select Case x._GameType2
                                                 Case "언오버"
                                                     x._ResultVal = _GameList(lstCnt).gUnderOver
                                                     SetVirtual패턴설정SubItem(x._RowCnt, 6, _GameList(lstCnt).gUnderOver)
                                                 Case "홀짝"
                                                     x._ResultVal = _GameList(lstCnt).gOddEven
                                                     SetVirtual패턴설정SubItem(x._RowCnt, 6, _GameList(lstCnt).gOddEven)
                                             End Select
                                         Case "파워볼"
                                             Select Case x._GameType2
                                                 Case "언오버"
                                                     x._ResultVal = _GameList(lstCnt).pUnderOver
                                                     SetVirtual패턴설정SubItem(x._RowCnt, 6, _GameList(lstCnt).pUnderOver)
                                                 Case "홀짝"
                                                     x._ResultVal = _GameList(lstCnt).pOddEven
                                                     SetVirtual패턴설정SubItem(x._RowCnt, 6, _GameList(lstCnt).pOddEven)
                                             End Select
                                     End Select
                                 Case 1
                                     x._ResultVal = _GameList(lstCnt).gDJS
                                     SetVirtual패턴설정SubItem(x._RowCnt, 6, _GameList(lstCnt).gDJS)
                                 Case 2, 3
                                     x._ResultVal = $"{_GameList(lstCnt).gUnderOver}{_GameList(lstCnt).gOddEven}"
                                     SetVirtual패턴설정SubItem(x._RowCnt, 6, $"{_GameList(lstCnt).gUnderOver}{_GameList(lstCnt).gOddEven}")
                                     type3str = $"{_GameList(lstCnt).gUnderOver}{_GameList(lstCnt).gOddEven}"
                             End Select

                             If x._BetVal = x._ResultVal Then
                                 Select Case x._BetType
                                     Case 0
                                         x._ResultAmount = x._Amount * 1.97 - x._Amount
                                     Case 1
                                         If x._ResultVal = "중" Then
                                             x._ResultAmount = x._Amount * 2.4 - x._Amount
                                         Else
                                             x._ResultAmount = x._Amount * 2.5 - x._Amount
                                         End If
                                     Case 2, 3
                                         x._ResultAmount = x._Amount * 3.7 - x._Amount
                                 End Select
                             Else
                                 x._ResultAmount = -x._Amount
                             End If

                             Dim iw As Integer = 0
                             Dim id As Integer = 0
                             Dim il As Integer = 0

                             If x._ResultAmount < 0 Then
                                 il = 1
                             Else
                                 iw = 1
                             End If

                             Dim itemData = GetVirtual패턴설정Item(x._RowCnt)
                             Dim sArr2_() As String = Split(itemData(4), "/")

                             sArr2_(0) = CInt(sArr2_(0)) + iw
                             sArr2_(1) = CInt(sArr2_(1)) + id
                             sArr2_(2) = CInt(sArr2_(2)) + il

                             _iw += iw
                             _id += id
                             _il += il

                             SetVirtual패턴설정SubItem(x._RowCnt, 4, Join(sArr2_, "/"))
                         End Sub)

        lResult = _BetList.Sum(Function(x) x._ResultAmount)

        Dim tAmount As Long = _BetList.Sum(Function(x) x._ResultAmount)

        If b가상Checked Then
            SetAmount(tAmount)
        End If

        If cboBetType.SelectedIndex = 3 Then
            For iii = 0 To GetVirtual패턴설정Count() - 1
                Dim cRow = _BetList.Where(Function(x) x._RowCnt = iii).ToList

                If cRow.Count > 0 Then
                    Dim itemData = GetVirtual패턴설정Item(iii)
                    Dim sArr() As String = Split(itemData(3), "-")
                    Dim pat_key As String = itemData(10)

                    Dim _Cur = _all_cFarePattern.patternList.Where(Function(xx) xx.reckey = pat_key).ToList
                    Dim _game_pattern As List(Of HS_Game_Line_Data) = chkPattern_Multi(_Cur.First.pattern_value)

                    Dim cRow2 = cRow.Where(Function(x) x._BetVal = x._ResultVal).ToList

                    Select Case _game_pattern(sArr.First - 1).GamePatterns.Count
                        Case 1
                            If cRow2.Count > 0 Then
                                SetVirtual패턴설정SubItem(iii, 3, "1-1")
                            Else
                                If sArr.First + 1 > _game_pattern.Count Then
                                    SetVirtual패턴설정SubItem(iii, 3, "1-1")
                                Else
                                    SetVirtual패턴설정SubItem(iii, 3, sArr.First + 1 & "-1")
                                End If
                            End If
                        Case Else
                            If cRow2.Count > 0 Then
                                If sArr.Last = _game_pattern(sArr.First - 1).GamePatterns.Count Then
                                    SetVirtual패턴설정SubItem(iii, 3, "1-1")
                                Else
                                    SetVirtual패턴설정SubItem(iii, 3, sArr.First & "-" & sArr.Last + 1)
                                End If
                            Else
                                If sArr.First + 1 > _game_pattern.Count Then
                                    SetVirtual패턴설정SubItem(iii, 3, "1-1")
                                Else
                                    SetVirtual패턴설정SubItem(iii, 3, sArr.First + 1 & "-1")
                                End If
                            End If
                    End Select

                    SetVirtual패턴설정SubItem(iii, 5, "")
                    SetVirtual패턴설정SubItem(iii, 6, "")
                    SetVirtual패턴설정SubItem(iii, 7, "0")
                End If
            Next
        Else
            _BetList.ForEach(Sub(x)
                                 Dim itemData = GetVirtual패턴설정Item(x._RowCnt)
                                 Dim sArr() As String = Split(itemData(3), "-")
                                 Dim pat_key As String = itemData(10)

                                 Dim _Cur = _all_cFarePattern.patternList.Where(Function(xx) xx.reckey = pat_key).ToList
                                 Dim _game_pattern As List(Of HS_Game_Pattern) = chkPattern(_Cur.First.pattern_value)
                                 Select Case _game_pattern(sArr.First - 1).money_list.Count
                                     Case 1
                                         If itemData(5) = itemData(6) Then
                                             If _game_pattern(sArr.First - 1).money_list.First.movelist.wLevel = "" Then
                                                 SetVirtual패턴설정SubItem(x._RowCnt, 3, "1-1")
                                             Else
                                                 SetVirtual패턴설정SubItem(x._RowCnt, 3, _game_pattern(sArr.First - 1).money_list.First.movelist.wLevel)
                                             End If
                                         Else
                                             If sArr.First + 1 > _game_pattern.Count Then
                                                 If _game_pattern(sArr.First - 1).money_list.First.movelist.lLevel = "" Then
                                                     SetVirtual패턴설정SubItem(x._RowCnt, 3, "1-1")
                                                 Else
                                                     SetVirtual패턴설정SubItem(x._RowCnt, 3, _game_pattern(sArr.First - 1).money_list.First.movelist.lLevel)
                                                 End If
                                                 'lst패턴설정.Items(x._RowCnt).SubItems(4).Text = "1-1"
                                             Else
                                                 If _game_pattern(sArr.First - 1).money_list.First.movelist.lLevel = "" Then
                                                     SetVirtual패턴설정SubItem(x._RowCnt, 3, sArr.First + 1 & "-1")
                                                 Else
                                                     SetVirtual패턴설정SubItem(x._RowCnt, 3, _game_pattern(sArr.First - 1).money_list.First.movelist.lLevel)
                                                 End If
                                             End If
                                         End If
                                     Case Else
                                         If itemData(5) = itemData(6) Then
                                             If sArr.Last = _game_pattern(sArr.First - 1).money_list.Count Then
                                                 If _game_pattern(sArr.First - 1).money_list.First.movelist.wLevel = "" Then
                                                     SetVirtual패턴설정SubItem(x._RowCnt, 3, "1-1")
                                                 Else
                                                     SetVirtual패턴설정SubItem(x._RowCnt, 3, _game_pattern(sArr.First - 1).money_list.First.movelist.wLevel)
                                                 End If
                                                 'lst패턴설정.Items(x._RowCnt).SubItems(4).Text = "1-1"
                                             Else
                                                 If _game_pattern(sArr.First - 1).money_list.First.movelist.wLevel = "" Then
                                                     SetVirtual패턴설정SubItem(x._RowCnt, 3, sArr.First & "-" & sArr.Last + 1)
                                                 Else
                                                     SetVirtual패턴설정SubItem(x._RowCnt, 3, _game_pattern(sArr.First - 1).money_list.First.movelist.wLevel)
                                                 End If
                                             End If
                                         Else
                                             If sArr.First + 1 > _game_pattern.Count Then
                                                 If _game_pattern(sArr.First - 1).money_list.First.movelist.lLevel = "" Then
                                                     SetVirtual패턴설정SubItem(x._RowCnt, 3, "1-1")
                                                 Else
                                                     SetVirtual패턴설정SubItem(x._RowCnt, 3, _game_pattern(sArr.First - 1).money_list.First.movelist.lLevel)
                                                 End If
                                                 'lst패턴설정.Items(x._RowCnt).SubItems(4).Text = "1-1"
                                             Else
                                                 If _game_pattern(sArr.First - 1).money_list.First.movelist.lLevel = "" Then
                                                     SetVirtual패턴설정SubItem(x._RowCnt, 3, sArr.First + 1 & "-1")
                                                 Else
                                                     SetVirtual패턴설정SubItem(x._RowCnt, 3, _game_pattern(sArr.First - 1).money_list.First.movelist.lLevel)
                                                 End If
                                             End If
                                         End If

                                 End Select
                                 SetVirtual패턴설정SubItem(x._RowCnt, 5, "")
                                 SetVirtual패턴설정SubItem(x._RowCnt, 6, "")
                                 SetVirtual패턴설정SubItem(x._RowCnt, 7, "0")
                             End Sub)
        End If

        SetListbox(String.Format("[{1}]    결과{2} = 획득머니 : {0}", lResult, Now.ToString("HH:mm"), type3str))

        ' Clear betting display fields for all items
        For iii = 0 To GetVirtual패턴설정Count() - 1
            SetVirtual패턴설정SubItem(iii, 5, "")
            SetVirtual패턴설정SubItem(iii, 6, "")
            SetVirtual패턴설정SubItem(iii, 7, "0")
        Next

        _BetList = New List(Of _Bet)

    End Sub

    Private Async Sub Button1_Click(sender As Object, e As EventArgs) Handles Button1.Click
        Dim scriptTemplate As String = "
                (function() {
        var btn = document.querySelector('[data-role=''history-button'']');
        if (btn) btn.click();
    })();
    "

        Await SendClickAsync("powerball/player/game", scriptTemplate)

    End Sub

    Private Sub Button2_Click(sender As Object, e As EventArgs) Handles Button2.Click
        WebView21.CoreWebView2.Navigate(txtUrlBox.Text)
    End Sub

    Private Sub Button3_Click(sender As Object, e As EventArgs) Handles Button3.Click
        ClickAtPointSafe(557, 735)
    End Sub

    Private Sub Timer1_Tick(sender As Object, e As EventArgs) Handles Timer1.Tick
        Timer1.Enabled = False

        ClickAtPointSafe(660, 350)

        Timer1.Enabled = True
    End Sub

    Private Sub Button4_Click(sender As Object, e As EventArgs) Handles Button4.Click
        ClickAtPointSafe(490, 550)
    End Sub

    Private Sub Button5_Click(sender As Object, e As EventArgs) Handles Button5.Click
        ClickAtPointSafe(510, 735)
    End Sub

    Private Sub frmMain_FormClosed(sender As Object, e As FormClosedEventArgs) Handles Me.FormClosed
        frmLogin.Close()

    End Sub

#Region "[Virtual ListView Event Handlers]"
    Private Sub lst패턴설정_RetrieveVirtualItem(sender As Object, e As RetrieveVirtualItemEventArgs) Handles lst패턴설정.RetrieveVirtualItem
        SyncLock dataLock
            If e.ItemIndex >= 0 AndAlso e.ItemIndex < lst패턴설정Data.Count Then
                e.Item = New ListViewItem(lst패턴설정Data(e.ItemIndex))
            Else
                e.Item = New ListViewItem(New String(10) {})
            End If
        End SyncLock
    End Sub

    Private Sub lst단계별통계_RetrieveVirtualItem(sender As Object, e As RetrieveVirtualItemEventArgs) Handles lst단계별통계.RetrieveVirtualItem
        SyncLock dataLock
            If e.ItemIndex >= 0 AndAlso e.ItemIndex < lst단계별통계Data.Count Then
                e.Item = New ListViewItem(lst단계별통계Data(e.ItemIndex))
            Else
                e.Item = New ListViewItem(New String(6) {})
            End If
        End SyncLock
    End Sub

    Private Sub UpdateVirtualListView(ByRef listView As ListView, ByRef dataList As List(Of String()), newSize As Integer)
        SyncLock dataLock
            listView.VirtualListSize = newSize
        End SyncLock
        If listView.InvokeRequired Then
            listView.Invoke(Sub() listView.Invalidate())
        Else
            listView.Invalidate()
        End If
    End Sub

    Private Sub AddVirtual패턴설정Item(arr As String())
        SyncLock dataLock
            lst패턴설정Data.Add(arr.Clone())
        End SyncLock
        UpdateVirtualListView(lst패턴설정, lst패턴설정Data, lst패턴설정Data.Count)
    End Sub

    Private Sub AddVirtual단계별통계Item(arr As String())
        SyncLock dataLock
            lst단계별통계Data.Add(arr.Clone())
        End SyncLock
        UpdateVirtualListView(lst단계별통계, lst단계별통계Data, lst단계별통계Data.Count)
    End Sub

    Private Sub ClearVirtual패턴설정()
        SyncLock dataLock
            lst패턴설정Data.Clear()
        End SyncLock
        UpdateVirtualListView(lst패턴설정, lst패턴설정Data, 0)
    End Sub

    Private Sub ClearVirtual단계별통계()
        SyncLock dataLock
            lst단계별통계Data.Clear()
        End SyncLock
        UpdateVirtualListView(lst단계별통계, lst단계별통계Data, 0)
    End Sub

    Private Function GetVirtual패턴설정Item(index As Integer) As String()
        SyncLock dataLock
            If index >= 0 AndAlso index < lst패턴설정Data.Count Then
                Return lst패턴설정Data(index).Clone()
            End If
        End SyncLock
        Return New String(10) {}
    End Function

    Private Function GetVirtual단계별통계Item(index As Integer) As String()
        SyncLock dataLock
            If index >= 0 AndAlso index < lst단계별통계Data.Count Then
                Return lst단계별통계Data(index).Clone()
            End If
        End SyncLock
        Return New String(6) {}
    End Function

    Private Sub SetVirtual패턴설정SubItem(index As Integer, subIndex As Integer, value As String)
        SyncLock dataLock
            If index >= 0 AndAlso index < lst패턴설정Data.Count AndAlso subIndex >= 0 AndAlso subIndex < lst패턴설정Data(index).Length Then
                lst패턴설정Data(index)(subIndex) = value
            End If
        End SyncLock
        If lst패턴설정.InvokeRequired Then
            lst패턴설정.Invoke(Sub() lst패턴설정.RedrawItems(index, index, False))
        Else
            lst패턴설정.RedrawItems(index, index, False)
        End If
    End Sub

    Private Sub SetVirtual단계별통계SubItem(index As Integer, subIndex As Integer, value As String)
        SyncLock dataLock
            If index >= 0 AndAlso index < lst단계별통계Data.Count AndAlso subIndex >= 0 AndAlso subIndex < lst단계별통계Data(index).Length Then
                lst단계별통계Data(index)(subIndex) = value
            End If
        End SyncLock
        If lst단계별통계.InvokeRequired Then
            lst단계별통계.Invoke(Sub() lst단계별통계.RedrawItems(index, index, False))
        Else
            lst단계별통계.RedrawItems(index, index, False)
        End If
    End Sub

    Private Function GetVirtual패턴설정Count() As Integer
        SyncLock dataLock
            Return lst패턴설정Data.Count
        End SyncLock
    End Function

    Private Function GetVirtual단계별통계Count() As Integer
        SyncLock dataLock
            Return lst단계별통계Data.Count
        End SyncLock
    End Function
#End Region

End Class
#Region "[웹소켓 클래스]"

''' <summary>
''' 웹소켓 정보를 저장하기 위한 클래스
''' </summary>
Public Class WebSocketInfo
    Public Property RequestId As String
    Public Property Url As String
    Public Property CreatedTime As DateTime
    Public Property Headers As Object
    Public Property Frames As New List(Of String)
    Public Property FrameId As String ' << 추가된 속성
End Class

''' <summary>
''' 새 창 정보를 저장하기 위한 클래스
''' </summary>
Public Class NewWindowInfo
    Public Property WebView As WebView2
    Public Property Form As Form
    Public Property WindowId As String
End Class

''' <summary>
''' iframe 정보를 저장하기 위한 클래스
''' </summary>
Public Class FrameInfo
    Public Property FrameId As String
    Public Property ParentFrameId As String
    Public Property Url As String
    Public Property Name As String
End Class

#End Region