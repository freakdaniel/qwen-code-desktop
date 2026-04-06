import { Box } from '@chakra-ui/react';
import { useState } from 'react';
import { useBootstrap } from '@/hooks/useBootstrap';
import Sidebar from './Sidebar';
import ChatArea from './ChatArea';

export default function MainLayout() {
  const [isSidebarOpen, setIsSidebarOpen] = useState(true);
  const { bootstrap, activeTurnSessions } = useBootstrap();

  return (
    <Box h="100vh" w="100vw" overflow="hidden" bg="gray.900">
      <Sidebar
        isOpen={isSidebarOpen}
        onClose={() => setIsSidebarOpen(false)}
        onToggle={() => setIsSidebarOpen(!isSidebarOpen)}
        sessions={bootstrap?.recentSessions || []}
        activeTurnSessions={activeTurnSessions}
      />

      <Box
        ml={isSidebarOpen ? "260px" : "0"}
        h="100vh"
        transition="margin-left 0.3s ease"
      >
        <ChatArea
          isSidebarOpen={isSidebarOpen}
          onToggleSidebar={() => setIsSidebarOpen(true)}
        />
      </Box>
    </Box>
  );
}
