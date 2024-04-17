using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WindowsFormsApp1
{
    public partial class GameForm : Form
    {
        private Thread rabbitThread;
        private Thread carrotThread;
        private Thread trapThread;
        private readonly object syncLock = new object();


        private Point targetRabbitPosition;
        private const int moveDuration = 200; // Движение за 500 мс
        private DateTime moveStartTime;
        private Point startPosition;
        private bool isMoving = false;


        private bool isGameRunning;
        private CancellationTokenSource cancellationTokenSource;
        private Point rabbitPosition;
        private List<Rectangle> carrots;
        private List<Point> traps; // Хранит позиции ловушек

        private const int CellSize = 50;
        private const int GridWidth = 6;
        private const int GridHeight = 6;

        private Image rabbitImage;
        private Image carrotImage;
        private Image cellBackgroundImage;
        private Image trapImage;



        public GameForm()
        {
            InitializeComponent();
            this.DoubleBuffered = true;
            this.ClientSize = new Size(CellSize * GridWidth, CellSize * GridHeight);
            cancellationTokenSource = new CancellationTokenSource();

            rabbitPosition = new Point(100, 100);

            InitializeLevel();
            this.KeyDown += new KeyEventHandler(GameForm_KeyDown);

        }

        private void InitializeLevel()
        {
            isGameRunning = true;
            rabbitPosition = new Point(250, 200);

            rabbitImage = Image.FromFile("D:\\C#3\\WindowsFormsApp1\\Properties\\Rabbit3.png");
            carrotImage = Image.FromFile("D:\\C#3\\WindowsFormsApp1\\Properties\\Carrot4.png");
            cellBackgroundImage = Image.FromFile("D:\\C#3\\WindowsFormsApp1\\Properties\\Grass2.png");
            trapImage = Image.FromFile("D:\\C#3\\WindowsFormsApp1\\Properties\\Trap.png");

            carrots = new List<Rectangle>();

            for (int i = 0; i < GridHeight; i++)
            {
                for (int j = 0; j < GridWidth; j++)
                {
                    if ((i + j) % 2 == 0)
                    {
                        carrots.Add(new Rectangle(j * CellSize, i * CellSize, CellSize, CellSize));
                    }
                }
            }

            traps = new List<Point>();
            Random random = new Random();
            while (traps.Count < 4)
            {
                Point trapPosition = new Point(random.Next(0, GridWidth) * CellSize, random.Next(0, GridHeight) * CellSize);
                // Проверка, что ловушка не находится в той же позиции, что и морковка или кролик
                if (!traps.Contains(trapPosition) &&
                    !carrots.Exists(c => c.Contains(trapPosition)) &&
                    trapPosition != rabbitPosition)
                {
                    traps.Add(trapPosition);
                }
            }

        }

       
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;

            // Сначала рисуем фон для каждой клетки.
            for (int i = 0; i < GridWidth; i++)
            {
                for (int j = 0; j < GridHeight; j++)
                {
                    g.DrawImage(cellBackgroundImage, i * CellSize, j * CellSize, CellSize, CellSize);
                }
            }

            foreach (Point trap in traps)
            {
                DrawTrap(e.Graphics, trap.X, trap.Y);
            }

            // Затем рисуем морковки.
            foreach (var carrot in carrots)
            {
                DrawCarrot(g, carrot.X, carrot.Y);
            }

            // В конце рисуем зайца.
            DrawRabbit(g, rabbitPosition.X, rabbitPosition.Y);
        }

        private void DrawTrap(Graphics g, int x, int y)
        {
            g.DrawImage(trapImage, new Rectangle(x, y, CellSize, CellSize));
        }

        private void GameForm_Load(object sender, EventArgs e)
        {
            StartGame();
        }

        private void GameForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (!isGameRunning || isMoving) return; // Игнорируем ввод, если игра остановлена или идёт анимация

            var newRabbitPosition = rabbitPosition;
            switch (e.KeyCode)
            {
                case Keys.Up:
                    newRabbitPosition.Y -= CellSize;
                    break;
                case Keys.Down:
                    newRabbitPosition.Y += CellSize;
                    break;
                case Keys.Left:
                    newRabbitPosition.X -= CellSize;
                    break;
                case Keys.Right:
                    newRabbitPosition.X += CellSize;
                    break;
            }

            if (newRabbitPosition.X >= 0 && newRabbitPosition.X < CellSize * GridWidth &&
                newRabbitPosition.Y >= 0 && newRabbitPosition.Y < CellSize * GridHeight &&
                !isMoving) // Проверяем, что анимация не выполняется
            {
                startPosition = rabbitPosition;
                targetRabbitPosition = newRabbitPosition;
                moveStartTime = DateTime.Now;
                isMoving = true; // Запускаем анимацию
            }
        }


        private void WinGame()
        {
            isGameRunning = false;
            MessageBox.Show("Вы выиграли! Все морковки собраны.");
            Close();
        }


        private void StartGame()
        {
            isGameRunning = true;

            rabbitThread = new Thread(RabbitMovement) { IsBackground = true };
            carrotThread = new Thread(CheckCarrot) { IsBackground = true };

            rabbitThread.Start();
            carrotThread.Start();

            trapThread = new Thread(TrapBehavior) { IsBackground = true };
            trapThread.Start();

        }

        private void TrapBehavior()
        {
            while (isGameRunning && !cancellationTokenSource.IsCancellationRequested)
            {
                lock (syncLock)
                {
                    foreach (Point trap in traps)
                    {
                        if (rabbitPosition == trap)
                        {
                            Invoke((MethodInvoker)GameOver);
                            return; // Завершаем поток, так как кролик пойман в ловушку
                        }
                    }
                }
                Thread.Sleep(100); // Проверка каждые 100 миллисекунд
            }
        }

        private void RabbitMovement()
        {
            while (isGameRunning && !cancellationTokenSource.IsCancellationRequested)
            {
                if (isMoving)
                {
                    Invoke((MethodInvoker)delegate
                    {
                        var currentTime = DateTime.Now;
                        var elapsed = (currentTime - moveStartTime).TotalMilliseconds;
                        if (elapsed < moveDuration)
                        {
                            // Плавное перемещение зайца
                            double t = elapsed / moveDuration;
                            rabbitPosition = new Point(
                                startPosition.X + (int)((targetRabbitPosition.X - startPosition.X) * t),
                                startPosition.Y + (int)((targetRabbitPosition.Y - startPosition.Y) * t)
                            );
                        }
                        else
                        {
                            // Убедимся, что заяц в конечном итоге оказывается в центре клетки
                            rabbitPosition = targetRabbitPosition;
                            isMoving = false; // Движение завершено
                        }
                        Invalidate(); // Перерисовка формы
                    });
                }
                Thread.Sleep(10); // Для плавности анимации
            }
        }


        private void CheckCarrot()
        {
            while (isGameRunning && !cancellationTokenSource.IsCancellationRequested)
            {
                Invoke((MethodInvoker)delegate
                {
                    CheckRabbitCarrotCollision();
                });
                Thread.Sleep(100);
            }
        }


        private async void CheckRabbitCarrotCollision()
        {
            Rectangle rabbitRectangle = new Rectangle(rabbitPosition, new Size(CellSize, CellSize));
            bool eaten = false;

            for (int i = carrots.Count - 1; i >= 0; i--)
            {
                if (rabbitRectangle.IntersectsWith(carrots[i]))
                {
                    eaten = true;
                    carrots.RemoveAt(i);
                }
            }

            foreach (Point trap in traps)
            {
                if (rabbitPosition == trap)
                {
                    GameOver();
                    return; // Выход, так как игра закончена
                }
            }

            if (eaten)
            {
                if (carrots.Count == 0)
                {
                    await Task.Delay(150);
                    WinGame();
                }
            }
        }

        private void GameOver()
        {
            if (InvokeRequired)
            {
                Invoke((MethodInvoker)delegate { GameOver(); });
                return;
            }
            isGameRunning = false;
            MessageBox.Show("К сожалению, игра закончена. Вы наступили на ловушку!");
            Close();
        }

        private void DrawRabbit(Graphics g, int x, int y)
        {
            g.DrawImage(rabbitImage, new Rectangle(x, y, CellSize, CellSize));
        }

        private void DrawCarrot(Graphics g, int x, int y)
        {
            g.DrawImage(carrotImage, new Rectangle(x, y, CellSize, CellSize));

        }

    }
}

