import { createStyles, Theme } from '@material-ui/core';

export const globalStyles = {
	navigation: {
		fontSize: 15,
		fontWeight: 'lighter',
		color: '#777',
		paddingBottom: 15,
		display: 'block'
	},
	title: {
		fontSize: 24,
		fontWeight: 'lighter',
		marginBottom: 20
	},
	paper: {
		padding: 30
	},
	clear: {
		clear: 'both'
	}
};

export const globalStylesRecord = (theme: Theme) => createStyles({
	navigation: {
		fontSize: 15,
		fontWeight: 'lighter',
		color: '#777',
		paddingBottom: 15,
		display: 'block'
	},
	title: {
		fontSize: 24,
		fontWeight: 'lighter',
		marginBottom: 20
	},
	paper: {
		padding: 30
	},
	clear: {
		clear: 'both'
	}
});

