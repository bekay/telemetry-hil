################################################################################
# Automatically-generated file. Do not edit!
################################################################################

# Add inputs and outputs from these tool invocations to the build variables 
C_SRCS += \
../app.c \
../main.c 

OBJS += \
./app.o \
./main.o 

C_DEPS += \
./app.d \
./main.d 


# Each subdirectory must supply rules for building sources it contributes
app.o: ../app.c subdir.mk
	@echo 'Building file: $<'
	@echo 'Invoking: GNU ARM C Compiler'
	arm-none-eabi-gcc -g -gdwarf-2 -mcpu=cortex-m4 -mthumb -std=c99 '-DDEBUG_EFM=1' '-DEFM32PG1B200F256GM48=1' '-DHFXO_FREQ=40000000' '-DSL_BOARD_NAME="BRD2500A"' '-DSL_BOARD_REV="A01"' '-DSL_COMPONENT_CATALOG_PRESENT=1' -I"C:\Users\bck10\source\repos\radar_sim\config" -I"C:\Users\bck10\source\repos\radar_sim\autogen" -I"C:\Users\bck10\source\repos\radar_sim" -I"C:/Users/bck10/SimplicityStudio/SDKs/gecko_sdk//platform/Device/SiliconLabs/EFM32PG1B/Include" -I"C:/Users/bck10/SimplicityStudio/SDKs/gecko_sdk//platform/common/inc" -I"C:/Users/bck10/SimplicityStudio/SDKs/gecko_sdk//hardware/board/inc" -I"C:/Users/bck10/SimplicityStudio/SDKs/gecko_sdk//platform/CMSIS/Core/Include" -I"C:/Users/bck10/SimplicityStudio/SDKs/gecko_sdk//platform/service/device_init/inc" -I"C:/Users/bck10/SimplicityStudio/SDKs/gecko_sdk//platform/emlib/inc" -I"C:/Users/bck10/SimplicityStudio/SDKs/gecko_sdk//platform/driver/leddrv/inc" -I"C:/Users/bck10/SimplicityStudio/SDKs/gecko_sdk//platform/common/toolchain/inc" -I"C:/Users/bck10/SimplicityStudio/SDKs/gecko_sdk//platform/service/system/inc" -I"C:/Users/bck10/SimplicityStudio/SDKs/gecko_sdk//platform/service/sleeptimer/inc" -Os -Wall -Wextra -ffunction-sections -fdata-sections -imacrossl_gcc_preinclude.h -mfpu=fpv4-sp-d16 -mfloat-abi=softfp --specs=nano.specs -c -fmessage-length=0 -MMD -MP -MF"app.d" -MT"$@" -o "$@" "$<"
	@echo 'Finished building: $<'
	@echo ' '

main.o: ../main.c subdir.mk
	@echo 'Building file: $<'
	@echo 'Invoking: GNU ARM C Compiler'
	arm-none-eabi-gcc -g -gdwarf-2 -mcpu=cortex-m4 -mthumb -std=c99 '-DDEBUG_EFM=1' '-DEFM32PG1B200F256GM48=1' '-DHFXO_FREQ=40000000' '-DSL_BOARD_NAME="BRD2500A"' '-DSL_BOARD_REV="A01"' '-DSL_COMPONENT_CATALOG_PRESENT=1' -I"C:\Users\bck10\source\repos\radar_sim\config" -I"C:\Users\bck10\source\repos\radar_sim\autogen" -I"C:\Users\bck10\source\repos\radar_sim" -I"C:/Users/bck10/SimplicityStudio/SDKs/gecko_sdk//platform/Device/SiliconLabs/EFM32PG1B/Include" -I"C:/Users/bck10/SimplicityStudio/SDKs/gecko_sdk//platform/common/inc" -I"C:/Users/bck10/SimplicityStudio/SDKs/gecko_sdk//hardware/board/inc" -I"C:/Users/bck10/SimplicityStudio/SDKs/gecko_sdk//platform/CMSIS/Core/Include" -I"C:/Users/bck10/SimplicityStudio/SDKs/gecko_sdk//platform/service/device_init/inc" -I"C:/Users/bck10/SimplicityStudio/SDKs/gecko_sdk//platform/emlib/inc" -I"C:/Users/bck10/SimplicityStudio/SDKs/gecko_sdk//platform/driver/leddrv/inc" -I"C:/Users/bck10/SimplicityStudio/SDKs/gecko_sdk//platform/common/toolchain/inc" -I"C:/Users/bck10/SimplicityStudio/SDKs/gecko_sdk//platform/service/system/inc" -I"C:/Users/bck10/SimplicityStudio/SDKs/gecko_sdk//platform/service/sleeptimer/inc" -Os -Wall -Wextra -ffunction-sections -fdata-sections -imacrossl_gcc_preinclude.h -mfpu=fpv4-sp-d16 -mfloat-abi=softfp --specs=nano.specs -c -fmessage-length=0 -MMD -MP -MF"main.d" -MT"$@" -o "$@" "$<"
	@echo 'Finished building: $<'
	@echo ' '


