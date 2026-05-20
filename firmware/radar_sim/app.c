#include "em_device.h"
#include "em_chip.h"
#include "em_cmu.h"
#include "em_gpio.h"
#include "em_usart.h"
#include "em_timer.h"
#include <string.h>
#include <stdio.h>

/* LED on PF4 for heartbeat indication */
#define LED0_PORT       gpioPortF
#define LED0_PIN        4

/* USART0 on PA0 (TX) and PA1 (RX) — routed through VCOM to PC */
#define USART_PORT      gpioPortA
#define USART_TX_PIN    0
#define USART_RX_PIN    1

/* VCOM enable on PA5 — must be HIGH to connect USART to USB bridge */
#define VCOM_ENABLE_PORT    gpioPortA
#define VCOM_ENABLE_PIN     5

/* PWM output on PA2 — TIMER0 CC0 location 2 */
#define PWM_PORT            gpioPortA
#define PWM_PIN             2
#define PWM_FREQ_HZ         1000
#define PWM_DUTY_PCT        50


static void init_clocks(void)
{
    /* USART0 and GPIO both sit on the HFPERCLK bus */
    CMU_ClockEnable(cmuClock_HFPER,  true);
    CMU_ClockEnable(cmuClock_GPIO,   true);
    CMU_ClockEnable(cmuClock_USART0, true);
    CMU_ClockEnable(cmuClock_TIMER0, true);
}

static void init_Gpio(void)
{
    /* USART TX — push-pull output */
    GPIO_PinModeSet(USART_PORT, USART_TX_PIN, gpioModePushPull, 1);

    /* USART RX — input with filter */
    GPIO_PinModeSet(USART_PORT, USART_RX_PIN, gpioModeInputPull, 1);

    /* VCOM enable — must be HIGH to route USART through USB bridge to PC */
    GPIO_PinModeSet(VCOM_ENABLE_PORT, VCOM_ENABLE_PIN, gpioModePushPull, 1);

    GPIO_PinModeSet(LED0_PORT, LED0_PIN, gpioModePushPull, 0);
}

static void init_Usart(void)
{
    USART_InitAsync_TypeDef init = USART_INITASYNC_DEFAULT;
    init.baudrate = 115200;

    USART_InitAsync(USART0, &init);

    /* Route USART0 TX and RX to PA0 and PA1 — location 0 */
    USART0->ROUTEPEN  =  USART_ROUTEPEN_TXPEN
                       | USART_ROUTEPEN_RXPEN;
    USART0->ROUTELOC0 =  USART_ROUTELOC0_TXLOC_LOC0
                       | USART_ROUTELOC0_RXLOC_LOC0;

    /* Enable RX data-valid interrupt and route it through NVIC */
    USART_IntEnable(USART0, USART_IEN_RXDATAV);
    NVIC_ClearPendingIRQ(USART0_RX_IRQn);
    NVIC_EnableIRQ(USART0_RX_IRQn);
}

void init_TIMER0(void)
{
    // Timer init struct
    TIMER_Init_TypeDef timerInit = TIMER_INIT_DEFAULT;
    timerInit.prescale = timerPrescale1024;
    timerInit.enable = false; // don't start yet

    TIMER_Init(TIMER0, &timerInit);

    // Set top value for ~1Hz
    // 14MHz / 1024 = 13671 Hz, top = 13671 for ~1 second
    TIMER_TopSet(TIMER0, 13671);

    // Clear any pending interrupts
    TIMER_IntClear(TIMER0, TIMER_IF_OF);

    // Enable overflow interrupt in timer
    TIMER_IntEnable(TIMER0, TIMER_IEN_OF);

    // Enable TIMER0 IRQ in NVIC
    NVIC_ClearPendingIRQ(TIMER0_IRQn);
    NVIC_EnableIRQ(TIMER0_IRQn);

    // Now start the timer
    TIMER_Enable(TIMER0, true);
}

static void uartSendByte(uint8_t byte)
{
    /* Wait until TX buffer is empty before writing */
    while (!(USART0->STATUS & USART_STATUS_TXBL));
    USART0->TXDATA = byte;

}

static void uartSendString(const char *str)
{
    while (*str)
      uartSendByte((uint8_t)*str++);
}

static uint8_t scenario = 0;
static uint32_t pulse_count = 0;

/* UART receive line buffer */
#define CMD_BUF_SIZE    32
static char    cmd_buf[CMD_BUF_SIZE];
static uint8_t cmd_idx = 0;

typedef struct {
    uint32_t timestamp_ms;      // time of detection
    uint16_t pulse_width_us;    // pulse width in microseconds
    uint8_t  amplitude;         // 0-255 signal strength
    uint8_t  quality;           // 0-100 signal quality score
    uint8_t  channel;           // which detection channel
} RadarDetection_t;


// Normal detection — good signal
void SendNormalDetection(uint32_t timestamp)
{
    RadarDetection_t det = {
        .timestamp_ms  = timestamp,
        .pulse_width_us = 10,      // 10 microsecond pulse
        .amplitude     = 200,      // strong signal
        .quality       = 95,       // high quality
        .channel       = 1
    };

    // Format: DET:<timestamp>,<width>,<amp>,<qual>,<ch>\r\n
    char buf[64];
    sprintf(buf, "DET:%lu,%u,%u,%u,%u\r\n",
        det.timestamp_ms,
        det.pulse_width_us,
        det.amplitude,
        det.quality,
        det.channel);
    uartSendString(buf);
}

// Degraded detection — noisy signal
void SendDegradedDetection(uint32_t timestamp)
{
    RadarDetection_t det = {
        .timestamp_ms  = timestamp,
        .pulse_width_us = 10,
        .amplitude     = 85,       // weak signal
        .quality       = 42,       // poor quality
        .channel       = 1
    };
    // Format: DET:<timestamp>,<width>,<amp>,<qual>,<ch>\r\n
        char buf[64];
        sprintf(buf, "DET:%lu,%u,%u,%u,%u\r\n",
            det.timestamp_ms,
            det.pulse_width_us,
            det.amplitude,
            det.quality,
            det.channel);
        uartSendString(buf);
}

// No detection — missed pulse
void SendNoDetection(uint32_t timestamp)
{
    char buf[32];
    sprintf(buf, "NOD:%lu\r\n", timestamp);
    uartSendString(buf);
}

void TIMER0_IRQHandler(void)
{
    TIMER_IntClear(TIMER0, TIMER_IF_OF);
    GPIO_PinOutToggle(LED0_PORT, LED0_PIN);

    pulse_count++;

    switch(scenario)
    {
        case 0:  // Normal operation — steady detections
          SendNormalDetection(pulse_count * 1000);
          break;

        case 1:  // Degraded — occasional missed pulses
          if (pulse_count % 5 == 0)
              SendNoDetection(pulse_count * 1000);
          else
              SendNormalDetection(pulse_count * 1000);
          break;

        case 2:  // Noisy environment — low quality signal
          SendDegradedDetection(pulse_count * 1000);
          break;

        case 3:  // Fault state — sensor error
          uartSendString("ERR:SENSOR_FAULT\r\n");
          break;
    }
}

void USART0_RX_IRQHandler(void)
{
    uint32_t flags = USART_IntGet(USART0);
    USART_IntClear(USART0, flags);

    if (!(flags & USART_IF_RXDATAV))
        return;

    char c = (char)USART_Rx(USART0);

    if (c == '\r' || c == '\n') {
        if (cmd_idx > 0) {
            cmd_buf[cmd_idx] = '\0';
            UART_CommandHandler(cmd_buf);
            cmd_idx = 0;
        }
    } else if (cmd_idx < CMD_BUF_SIZE - 1) {
        cmd_buf[cmd_idx++] = c;
    }
}

void UART_CommandHandler(char* cmd)
{
    if (strcmp(cmd, "ID?") == 0)
      uartSendString("RADAR-SIM-EFM32-001\r\n");

    else if (strcmp(cmd, "ST?") == 0)
      uartSendString("OK\r\n");

    else if (strncmp(cmd, "SC:", 3) == 0)
    {
        // Set scenario: SC:0, SC:1, SC:2, SC:3
        scenario = (uint8_t)(cmd[3] - '0');
        uartSendString("OK\r\n");
    }

    else if (strcmp(cmd, "RST") == 0)
    {
        scenario = 0;
        pulse_count = 0;
        uartSendString("OK\r\n");
    }

    //sl_led_toggle(&LED_INSTANCE);
}


void app_init(void)
{
  init_clocks();
  init_Gpio();
  init_Usart();
  init_TIMER0();
}

