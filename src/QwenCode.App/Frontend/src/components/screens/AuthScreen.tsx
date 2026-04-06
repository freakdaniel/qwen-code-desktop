import { Box, VStack, Heading, Text, Button, Tabs, TabList, TabPanels, Tab, TabPanel, FormControl, FormLabel, Input, useToast } from '@chakra-ui/react';
import { useState } from 'react';
import { motion } from 'framer-motion';
import { Lock, Key, Chrome } from 'lucide-react';

interface AuthScreenProps {
  onComplete: () => void;
}

export default function AuthScreen({ onComplete }: AuthScreenProps) {
  const [activeTab, setActiveTab] = useState(0);
  const [apiKey, setApiKey] = useState('');
  const toast = useToast();

  const handleOAuthLogin = () => {
    // In a real app, this would initiate Qwen OAuth flow
    console.log('Initiating Qwen OAuth...');
    // Simulate successful login
    setTimeout(() => {
      localStorage.setItem('qwen-auth-token', 'mock-qwen-token');
      onComplete();
    }, 1500);
  };

  const handleApiKeySubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!apiKey.trim()) {
      toast({
        title: 'API Key Required',
        description: 'Please enter your API key',
        status: 'error',
        duration: 3000,
        isClosable: true,
      });
      return;
    }

    // In a real app, this would validate the API key
    localStorage.setItem('openai-api-key', apiKey);
    toast({
      title: 'Authentication Successful',
      description: 'API key saved successfully',
      status: 'success',
      duration: 3000,
      isClosable: true,
    });
    setTimeout(() => {
      onComplete();
    }, 1500);
  };

  return (
    <motion.div
      initial={{ opacity: 0, y: 20 }}
      animate={{ opacity: 1, y: 0 }}
      exit={{ opacity: 0, y: -20 }}
      transition={{ duration: 0.3 }}
    >
      <Box minH="100vh" bg="gray.900" display="flex" alignItems="center" justifyContent="center" p={4}>
        <Box maxW="md" width="100%" bg="gray.800" borderRadius="xl" p={8} boxShadow="xl">
          <VStack spacing={6} align="stretch">
            <VStack spacing={2} align="center">
              <Box boxSize="16" bg="brand.500" borderRadius="lg" display="flex" alignItems="center" justifyContent="center">
                <Chrome size={32} color="white" />
              </Box>
              <Heading size="lg" color="white">Welcome to Qwen Code</Heading>
              <Text color="gray.400" textAlign="center">
                Connect with AI agents to create projects and solve tasks
              </Text>
            </VStack>

            <Tabs variant="soft-rounded" index={activeTab} onChange={(index) => setActiveTab(index)}>
              <TabList mb={4} justifyContent="center">
                <Tab color="gray.400" _selected={{ color: 'white', bg: 'brand.500' }}>
                  <Lock size={16} style={{ marginRight: '8px' }} />
                  Qwen OAuth
                </Tab>
                <Tab color="gray.400" _selected={{ color: 'white', bg: 'brand.500' }}>
                  <Key size={16} style={{ marginRight: '8px' }} />
                  API Key
                </Tab>
              </TabList>
              
              <TabPanels>
                <TabPanel p={0}>
                  <VStack spacing={4}>
                    <Button
                      leftIcon={<Chrome size={18} />}
                      colorScheme="brand"
                      variant="solid"
                      onClick={handleOAuthLogin}
                      size="lg"
                      width="100%"
                    >
                      Sign in with Qwen
                    </Button>
                    <Text fontSize="sm" color="gray.400" textAlign="center">
                      Securely authenticate with Qwen services
                    </Text>
                  </VStack>
                </TabPanel>
                
                <TabPanel p={0}>
                  <form onSubmit={handleApiKeySubmit}>
                    <VStack spacing={4}>
                      <FormControl>
                        <FormLabel color="white">API Key</FormLabel>
                        <Input
                          type="password"
                          placeholder="Enter your OpenAI API key"
                          value={apiKey}
                          onChange={(e) => setApiKey(e.target.value)}
                          bg="gray.700"
                          borderColor="gray.600"
                          color="white"
                          _placeholder={{ color: 'gray.400' }}
                        />
                      </FormControl>
                      
                      <Button
                        type="submit"
                        colorScheme="brand"
                        variant="solid"
                        size="lg"
                        width="100%"
                      >
                        Connect
                      </Button>
                      
                      <Text fontSize="sm" color="gray.400" textAlign="center">
                        Enter your API key to connect with OpenAI services
                      </Text>
                    </VStack>
                  </form>
                </TabPanel>
              </TabPanels>
            </Tabs>
          </VStack>
        </Box>
      </Box>
    </motion.div>
  );
}