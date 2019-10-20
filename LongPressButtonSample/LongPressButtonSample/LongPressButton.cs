//#define DETAIL_DEBUG

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace LongPressButtonSample
{
    /// <summary>
    /// 長押しでクリックイベントが発行されるボタンを提供します。
    /// </summary>
    public class LongPressButton : Button
    {
        #region フィールド

        /// <summary>
        /// 既定の長押し保持秒数を表します。
        /// </summary>
        private const int HOLD_SECONDS = 3;

        /// <summary>
        /// 長押しをカウントダウンするためのタイマーを保持します。
        /// </summary>
        private readonly DispatcherTimer countdownTimer = new DispatcherTimer();

        /// <summary>
        /// マウスの左ボタンのダウン イベント データを保持します。
        /// </summary>
        private MouseButtonEventArgs mouseLeftButtonDownEventArgs = null;

        /// <summary>
        /// スペース キーのダウン イベント データを保持します。
        /// </summary>
        private KeyEventArgs speceKeyDownEventArgs = null;

        /// <summary>
        /// リターン キーのダウン イベント データを保持します。
        /// </summary>
        private KeyEventArgs returnKeyDownEventArgs = null;

        #endregion

        #region 依存関係プロパティ

        /// <summary>
        /// IsLongPressEnabled 依存関係プロパティを識別します。このフィールドは読み取り専用です。
        /// </summary>
        public static readonly DependencyProperty IsLongPressEnabledProperty = DependencyProperty.Register(
            "IsLongPressEnabled",
            typeof(bool),
            typeof(LongPressButton),
            new PropertyMetadata(true));

        /// <summary>
        /// 長押し機能の有効状態を取得または設定します。
        /// </summary>
        [Description("長押し機能の有効状態を取得または設定します。")]
        public bool IsLongPressEnabled
        {
            get
            {
                return (bool)GetValue(IsLongPressEnabledProperty);
            }
            set
            {
                SetValue(IsLongPressEnabledProperty, value);
            }
        }

        /// <summary>
        /// HoldSeconds 依存関係プロパティを識別します。このフィールドは読み取り専用です。
        /// </summary>
        public static readonly DependencyProperty HoldSecondsProperty = DependencyProperty.Register(
            "HoldSeconds",
            typeof(int),
            typeof(LongPressButton),
            new PropertyMetadata(HOLD_SECONDS));

        /// <summary>
        /// 長押し保持秒数を取得または設定します。
        /// </summary>
        [Description("長押し保持秒数を取得または設定します。")]
        public int HoldSeconds
        {
            get
            {
                return (int)GetValue(HoldSecondsProperty);
            }
            set
            {
                SetValue(HoldSecondsProperty, value);
            }
        }

        /// <summary>
        /// LeftSeconds 依存関係プロパティの識別子を識別します。このフィールドは読み取り専用です。
        /// </summary>
        public static readonly DependencyPropertyKey LeftSecondsPropertyKey = DependencyProperty.RegisterReadOnly(
            "LeftSeconds",
            typeof(int?),
            typeof(LongPressButton),
            new PropertyMetadata(null));

        /// <summary>
        /// LeftSeconds 依存関係プロパティを識別します。このフィールドは読み取り専用です。
        /// </summary>
        public static readonly DependencyProperty LeftSecondsProperty = LeftSecondsPropertyKey.DependencyProperty;

        /// <summary>
        /// 長押しの残り秒数を取得します。カウントダウンをしていないときは <c>null</c> です。
        /// </summary>
        [Description("長押しの残り秒数を取得します。カウントダウンをしていないときは null です。")]
        public int? LeftSeconds
        {
            get
            {
                return (int?)GetValue(LeftSecondsProperty);
            }
            private set
            {
                SetValue(LeftSecondsPropertyKey, value);
            }
        }

        #endregion

        /// <summary>
        /// <see cref="LongPressButton"/> クラスの静的な初期化をします。
        /// </summary>
        static LongPressButton()
        {
            // 時間がたったらその時点でクリックとするため、規定の ClickMode を変更する。
            ClickModeProperty.OverrideMetadata(typeof(LongPressButton), new FrameworkPropertyMetadata(ClickMode.Press));

            // .NET Core 3.1 待ち。(AssemblyInfo.cs が自動生成になった影響で、反映されない。)
            // https://github.com/dotnet/wpf/issues/1699
#if false
            // 規定のスタイルを設定する。
            DefaultStyleKeyProperty.OverrideMetadata(typeof(LongPressButton), new FrameworkPropertyMetadata(typeof(LongPressButton)));
#endif
        }

        /// <summary>
        /// <see cref="LongPressButton"/> クラスの新しいインスタンスを初期化します。
        /// </summary>
        public LongPressButton()
        {
            // デザインモードでは処理を行わない。
            if (DesignerProperties.GetIsInDesignMode(this) != true)
            {
                Loaded += LongPressButton_Loaded;

                countdownTimer.Interval = TimeSpan.FromSeconds(1);
                countdownTimer.Tick += CuntdownTimer_Tick;
            }
        }

        /// <summary>
        /// 要素の配置、描画、および操作の準備が完了したときの処理を行います。
        /// </summary>
        /// <param name="sender">イベント ハンドラーがアタッチされているオブジェクト。</param>
        /// <param name="e">イベントのデータ。</param>
        private void LongPressButton_Loaded(object sender, RoutedEventArgs e)
        {
            // Loaded イベントは複数回連続して呼ばれる可能性があるため、ここでイベントを外しておく。
            Loaded -= LongPressButton_Loaded;
            Unloaded += LongPressButton_Unloaded;
        }

        /// <summary>
        /// 要素ツリーから要素が削除されたときの処理を行います。
        /// </summary>
        /// <param name="sender">イベント ハンドラーがアタッチされているオブジェクト。</param>
        /// <param name="e">イベントのデータ。</param>
        private void LongPressButton_Unloaded(object sender, RoutedEventArgs e)
        {
            // カウントダウンを停止する。
            StopCountdownIfStarted();

            Unloaded -= LongPressButton_Unloaded;
            Loaded += LongPressButton_Loaded;
        }

        /// <summary>
        /// カウントダウンが開始されていない場合は、カウントダウンを開始します。
        /// </summary>
        /// <returns>カウントダウンが開始された場合は <c>true</c>。既に開始されている場合か、開始する必要がなかった場合は <c>false</c>。</returns>
        private bool StartCountdownIfNotStaeted()
        {
            if (LeftSeconds == null)
            {
                // 長押し機能が無効の場合、または、長押し保持秒数が 0 以下の場合は、長押し処理の対象外。
                if ((IsLongPressEnabled == false) || (HoldSeconds < 1))
                {
                    // その場でイベントを発行する。
                    FireEvents();
                    return false;
                }

                LeftSeconds = HoldSeconds;
                countdownTimer.Start();
                return true;
            }

            return false;
        }

        /// <summary>
        /// カウントダウンが開始されている場合は、カウントダウンを停止します。
        /// </summary>
        /// <returns>カウントダウンが停止された場合は <c>true</c>。それ以外の場合は <c>false</c>。</returns>
        private bool StopCountdownIfStarted()
        {
            if (LeftSeconds != null)
            {
                countdownTimer.Stop();
                LeftSeconds = null;
                speceKeyDownEventArgs = null;
                returnKeyDownEventArgs = null;
                mouseLeftButtonDownEventArgs = null;
                return true;
            }

            return false;
        }

        /// <summary>
        /// このコントロールにフォーカスがある状態でユーザーがキーを押したときの処理をします。
        /// </summary>
        /// <param name="e">イベントのデータ。</param>
        protected override void OnKeyDown(KeyEventArgs e)
        {
            // 長押し中に保留しておきたいイベントを判定し、引数を保持する。
            if (e.Key == Key.Space)
            {
                // Alt + Space はシステムメニュー表示のため通す必要があるり、ここではそれ以外の場合を処理する。
                if ((Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Alt)) != ModifierKeys.Alt)
                {
                    if (e.IsRepeat == false)
                    {
                        // キーリピートでない最初のイベントは、IsRepeat == false。
                        // IsRepeat == true のイベントは、すべてキーリピートのイベントなので捨てる。
                        speceKeyDownEventArgs = e;
                        // カウントダウンを開始する。
                        StartCountdownIfNotStaeted();
                    }

                    // 基底クラスを呼ばない。
                    return;
                }
            }
            if (e.Key == Key.Return)
            {
                if (e.IsRepeat == false)
                {
                    // キーリピートでない最初のイベントは、IsRepeat == false。
                    // IsRepeat == true のイベントは、すべてキーリピートのイベントなので捨てる。
                    returnKeyDownEventArgs = e;
                    // カウントダウンを開始する。
                    StartCountdownIfNotStaeted();
                }

                // 基底クラスを呼ばない。
                return;
            }

            base.OnKeyDown(e);
        }

        /// <summary>
        /// 要素がキーボード フォーカスを失ったときの処理をします。
        /// </summary>
        /// <param name="e">イベントのデータ。</param>
        protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e)
        {
            // キーボードによる要因でカウントダウンが始まっている場合に限って止める。
            // この判定がないと、マウス長押し中に Tab キーでキーボードフォーカスを外すと
            // カウントダウンが誤停止してしまう。
            if ((speceKeyDownEventArgs != null) || (returnKeyDownEventArgs != null))
            {
                StopCountdownIfStarted();
            }

            base.OnLostKeyboardFocus(e);
        }

        /// <summary>
        /// このコントロールにフォーカスがある状態でユーザーがキーを離したときの処理をします。
        /// </summary>
        /// <param name="e">イベントのデータ。</param>
        protected override void OnKeyUp(KeyEventArgs e)
        {
            if (e.Key == Key.Space)
            {
                // Alt + Space はシステムメニュー表示のため通す必要があるり、ここではそれ以外の場合を処理する。
                if ((Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Alt)) != ModifierKeys.Alt)
                {
                    if (speceKeyDownEventArgs != null)
                    {
                        // キーボードによる要因でカウントダウンが始まっている場合に限って止める。
                        // この判定がないと、マウス長押し中に Tab キーでキーボードフォーカスを外すと
                        // カウントダウンが誤停止してしまう。
                        StopCountdownIfStarted();
                    }
                }
            }
            if (e.Key == Key.Return)
            {
                if (returnKeyDownEventArgs != null)
                {
                    // キーボードによる要因でカウントダウンが始まっている場合に限って止める。
                    // この判定がないと、マウス長押し中に Tab キーでキーボードフォーカスを外すと
                    // カウントダウンが誤停止してしまう。
                    StopCountdownIfStarted();
                }
            }

            base.OnKeyUp(e);
        }

        /// <summary>
        /// このコントロールの上にマウス ポインターがある間に、マウスの左ボタンが押されたときの処理をします。
        /// </summary>
        /// <param name="e">イベントのデータ。</param>
        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            // 長押し中に保留しておきたい引数を保持する。
            mouseLeftButtonDownEventArgs = e;
            // カウントダウンを開始する。
            StartCountdownIfNotStaeted();

            // 基底クラスを呼ばない。
        }

        /// <summary>
        /// マウスが要素から離れたときの処理をします。
        /// </summary>
        /// <param name="e">イベントのデータ。</param>
        protected override void OnMouseLeave(MouseEventArgs e)
        {
            // マウスによる要因でカウントダウンが始まっている場合に限って止める。
            // この判定がないと、キーボード長押し中にマウスをコントロール上から動かすと
            // カウントダウンが誤停止してしまう。
            if (mouseLeftButtonDownEventArgs != null)
            {
                StopCountdownIfStarted();
            }

            base.OnMouseLeave(e);
        }

        /// <summary>
        /// このコントロールの上にマウス ポインターがある間に、マウスの左ボタンが離されたときの処理をします。
        /// </summary>
        /// <param name="e">イベントのデータ。</param>
        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            // マウスによる要因でカウントダウンが始まっている場合に限って止める。
            // この判定がないと、キーボード長押し中にマウスをクリックすると
            // カウントダウンが誤停止してしまう。
            if (mouseLeftButtonDownEventArgs != null)
            {
                StopCountdownIfStarted();
            }

            base.OnMouseLeftButtonUp(e);
        }

        /// <summary>
        /// カウントダウン減算処理をします。
        /// </summary>
        /// <param name="sender">イベントのソース。</param>
        /// <param name="e">イベントのデータ。</param>
        private void CuntdownTimer_Tick(object sender, object e)
        {
            // タイムアップか判定する。
            if (LeftSeconds <= 1)
            {
                // タイムアップ処理を行う。
                countdownTimer.Stop();
                LeftSeconds = null;
                FireEvents();
            }
            else
            {
                // カウントダウン減算。
                LeftSeconds--;
            }
        }

        /// <summary>
        /// 保持しているイベントを発行します。
        /// </summary>
        private void FireEvents()
        {
            // 保持しているイベント データを放出する。
            if (mouseLeftButtonDownEventArgs != null)
            {
                base.OnMouseLeftButtonDown(mouseLeftButtonDownEventArgs);
                mouseLeftButtonDownEventArgs = null;
            }
            if (speceKeyDownEventArgs != null)
            {
                base.OnKeyDown(speceKeyDownEventArgs);
                speceKeyDownEventArgs = null;
            }
            if (returnKeyDownEventArgs != null)
            {
                base.OnKeyDown(returnKeyDownEventArgs);
                returnKeyDownEventArgs = null;
            }
        }

#if DETAIL_DEBUG
        /// <summary>
        /// クリックされたときの処理をします。
        /// </summary>
        protected override void OnClick()
        {
            Debug.WriteLine("OnClick");
            base.OnClick();
        }
#endif
    }
}
