import { Box, HStack, Text, IconButton } from '@chakra-ui/react';
import { Minus, Square, X, LayoutDashboard } from 'lucide-react';
import qwenLogo from '../../assets/qwen-logo.svg';
import { useTranslation } from 'react-i18next';

interface TitleBarProps {
  onToggleSidebar?: () => void;
}

export default function TitleBar({ onToggleSidebar }: TitleBarProps) {
  const { t } = useTranslation();

  const handleMinimize = () => {
    window.qwenDesktop?.minimizeWindow?.();
  };

  const handleMaximize = () => {
    window.qwenDesktop?.maximizeWindow?.();
  };

  const handleClose = () => {
    window.qwenDesktop?.closeWindow?.();
  };

  return (
    <Box
      h="36px"
      bg="gray.800"
      borderBottom="1px solid"
      borderColor="gray.700"
      display="flex"
      alignItems="center"
      justifyContent="space-between"
      sx={{ WebkitAppRegion: 'drag' } as React.CSSProperties}
      userSelect="none"
    >
      {/* Left side - toggle + logo + app title */}
      <HStack spacing={2} flex={1} pl={3}>
        {onToggleSidebar && (
          <IconButton
            aria-label={t('titlebar.toggleSidebar')}
            icon={<LayoutDashboard size={14} />}
            size="xs"
            variant="ghost"
            colorScheme="gray"
            color="gray.400"
            minW="28px"
            w="28px"
            h="28px"
            borderRadius="md"
            onClick={onToggleSidebar}
            _hover={{ bg: 'gray.700', color: 'white' }}
            _active={{ bg: 'gray.600' }}
            sx={{ WebkitAppRegion: 'no-drag' } as React.CSSProperties}
          />
        )}
        <Box
          w="18px"
          h="18px"
          display="flex"
          alignItems="center"
          justifyContent="center"
          overflow="hidden"
          flexShrink={0}
        >
          <img src={qwenLogo} alt="Qwen" style={{ width: '18px', height: '18px' }} />
        </Box>
        <Text
          fontSize="xs"
          fontWeight="medium"
          color="gray.400"
          sx={{ WebkitAppRegion: 'no-drag' } as React.CSSProperties}
        >
          {t('titlebar.appName')}
        </Text>
      </HStack>

      {/* Right side - window controls */}
      <HStack spacing={0} sx={{ WebkitAppRegion: 'no-drag' } as React.CSSProperties}>
        <IconButton
          aria-label={t('titlebar.minimize')}
          icon={<Minus size={13} />}
          size="xs"
          variant="ghost"
          colorScheme="gray"
          color="gray.400"
          minW="32px"
          w="32px"
          h="36px"
          borderRadius="0"
          p={0}
          onClick={handleMinimize}
          _hover={{ bg: 'gray.700', color: 'white' }}
          _active={{ bg: 'gray.600' }}
        />
        <IconButton
          aria-label={t('titlebar.maximize')}
          icon={<Square size={11} />}
          size="xs"
          variant="ghost"
          colorScheme="gray"
          color="gray.400"
          minW="32px"
          w="32px"
          h="36px"
          borderRadius="0"
          p={0}
          onClick={handleMaximize}
          _hover={{ bg: 'gray.700', color: 'white' }}
          _active={{ bg: 'gray.600' }}
        />
        <IconButton
          aria-label={t('titlebar.close')}
          icon={<X size={13} />}
          size="xs"
          variant="ghost"
          colorScheme="gray"
          color="gray.400"
          minW="32px"
          w="32px"
          h="36px"
          borderRadius="0"
          p={0}
          onClick={handleClose}
          _hover={{ bg: '#E81123', color: 'white' }}
          _active={{ bg: '#C50B1A' }}
        />
      </HStack>
    </Box>
  );
}
