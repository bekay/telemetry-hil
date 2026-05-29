################################################################################
# Automatically-generated file. Do not edit!
################################################################################

-include ../makefile.init

RM := rm -rf

# All of the sources participating in the build are defined here
-include sources.mk
-include gecko_sdk_4.5.0/platform/service/system/src/subdir.mk
-include gecko_sdk_4.5.0/platform/service/sleeptimer/src/subdir.mk
-include gecko_sdk_4.5.0/platform/service/device_init/src/subdir.mk
-include gecko_sdk_4.5.0/platform/emlib/src/subdir.mk
-include gecko_sdk_4.5.0/platform/driver/leddrv/src/subdir.mk
-include gecko_sdk_4.5.0/platform/common/toolchain/src/subdir.mk
-include gecko_sdk_4.5.0/platform/common/src/subdir.mk
-include gecko_sdk_4.5.0/platform/Device/SiliconLabs/EFM32PG1B/Source/subdir.mk
-include gecko_sdk_4.5.0/hardware/board/src/subdir.mk
-include autogen/subdir.mk
-include subdir.mk
-include objects.mk

ifneq ($(MAKECMDGOALS),clean)
ifneq ($(strip $(C_DEPS)),)
-include $(C_DEPS)
endif
endif

-include ../makefile.defs

OPTIONAL_TOOL_DEPS := \
$(wildcard ../makefile.defs) \
$(wildcard ../makefile.init) \
$(wildcard ../makefile.targets) \


BUILD_ARTIFACT_NAME := radar_sim
BUILD_ARTIFACT_EXTENSION := axf
BUILD_ARTIFACT_PREFIX :=
BUILD_ARTIFACT := $(BUILD_ARTIFACT_PREFIX)$(BUILD_ARTIFACT_NAME)$(if $(BUILD_ARTIFACT_EXTENSION),.$(BUILD_ARTIFACT_EXTENSION),)

# Add inputs and outputs from these tool invocations to the build variables 

# All Target
all: main-build

# Main-build Target
main-build: radar_sim.axf

# Tool invocations
radar_sim.axf: $(OBJS) $(USER_OBJS) makefile objects.mk $(OPTIONAL_TOOL_DEPS)
	@echo 'Building target: $@'
	@echo 'Invoking: GNU ARM C Linker'
	arm-none-eabi-gcc -g -gdwarf-2 -mcpu=cortex-m4 -mthumb -T "C:\Users\bck10\source\repos\radar_sim\autogen\linkerfile.ld" -Wl,--no-warn-rwx-segments -Xlinker --gc-sections -Xlinker -Map="radar_sim.map" -mfpu=fpv4-sp-d16 -mfloat-abi=softfp --specs=nano.specs -o radar_sim.axf -Wl,--start-group "./app.o" "./main.o" "./autogen/sl_board_default_init.o" "./autogen/sl_device_init_clocks.o" "./autogen/sl_event_handler.o" "./autogen/sl_simple_led_instances.o" "./gecko_sdk_4.5.0/hardware/board/src/sl_board_control_gpio.o" "./gecko_sdk_4.5.0/hardware/board/src/sl_board_init.o" "./gecko_sdk_4.5.0/platform/Device/SiliconLabs/EFM32PG1B/Source/startup_efm32pg1b.o" "./gecko_sdk_4.5.0/platform/Device/SiliconLabs/EFM32PG1B/Source/system_efm32pg1b.o" "./gecko_sdk_4.5.0/platform/common/src/sl_assert.o" "./gecko_sdk_4.5.0/platform/common/src/sl_syscalls.o" "./gecko_sdk_4.5.0/platform/common/toolchain/src/sl_memory.o" "./gecko_sdk_4.5.0/platform/driver/leddrv/src/sl_led.o" "./gecko_sdk_4.5.0/platform/driver/leddrv/src/sl_simple_led.o" "./gecko_sdk_4.5.0/platform/emlib/src/em_cmu.o" "./gecko_sdk_4.5.0/platform/emlib/src/em_core.o" "./gecko_sdk_4.5.0/platform/emlib/src/em_emu.o" "./gecko_sdk_4.5.0/platform/emlib/src/em_gpio.o" "./gecko_sdk_4.5.0/platform/emlib/src/em_msc.o" "./gecko_sdk_4.5.0/platform/emlib/src/em_rtcc.o" "./gecko_sdk_4.5.0/platform/emlib/src/em_system.o" "./gecko_sdk_4.5.0/platform/emlib/src/em_timer.o" "./gecko_sdk_4.5.0/platform/emlib/src/em_usart.o" "./gecko_sdk_4.5.0/platform/service/device_init/src/sl_device_init_dcdc_s1.o" "./gecko_sdk_4.5.0/platform/service/device_init/src/sl_device_init_emu_s1.o" "./gecko_sdk_4.5.0/platform/service/device_init/src/sl_device_init_hfxo_s1.o" "./gecko_sdk_4.5.0/platform/service/device_init/src/sl_device_init_lfxo_s1.o" "./gecko_sdk_4.5.0/platform/service/device_init/src/sl_device_init_nvic.o" "./gecko_sdk_4.5.0/platform/service/sleeptimer/src/sl_sleeptimer.o" "./gecko_sdk_4.5.0/platform/service/sleeptimer/src/sl_sleeptimer_hal_rtcc.o" "./gecko_sdk_4.5.0/platform/service/sleeptimer/src/sl_sleeptimer_hal_timer.o" "./gecko_sdk_4.5.0/platform/service/system/src/sl_system_init.o" "./gecko_sdk_4.5.0/platform/service/system/src/sl_system_process_action.o" -lgcc -lc -lm -lnosys -Wl,--end-group -Wl,--start-group -lgcc -lc -lnosys -Wl,--end-group
	@echo 'Finished building target: $@'
	@echo ' '

	@echo 'Building out file: radar_sim.out'
	arm-none-eabi-objcopy "radar_sim.axf" "radar_sim.out"
	@echo ' '

	@echo 'Building hex file: radar_sim.hex'
	arm-none-eabi-objcopy -O ihex "radar_sim.axf" "radar_sim.hex"
	@echo ' '

	@echo 'Building bin file: radar_sim.bin'
	arm-none-eabi-objcopy -O binary "radar_sim.axf" "radar_sim.bin"
	@echo ' '

	@echo 'Building s37 file: radar_sim.s37'
	arm-none-eabi-objcopy -O srec "radar_sim.axf" "radar_sim.s37"
	@echo ' '

	@echo 'Running size tool'
	arm-none-eabi-size "radar_sim.axf" -A
	@echo ' '

# Other Targets
clean:
	-@$(foreach FILE, $(EXECUTABLES), $(shell $(RM) $(FILE)) )
	-@$(foreach FILE, $(OBJS), $(shell $(RM) $(FILE)) )
	-@$(foreach FILE, $(C_DEPS), $(shell $(RM) $(FILE)) )
	-$(RM) radar_sim.axf
	-@echo ' '

.PHONY: all clean dependents main-build

-include ../makefile.targets
