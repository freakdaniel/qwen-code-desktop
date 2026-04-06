import { chakra } from '@chakra-ui/react';
import { motion } from 'framer-motion';

const MotionBox = chakra(motion.div, {
  shouldForwardProp: (prop) => 
    ['initial', 'animate', 'exit', 'transition'].includes(prop) ||
    prop.startsWith('while'),
});

export default MotionBox;