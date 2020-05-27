import Fab from "@material-ui/core/Fab";
import { createStyles, Theme, WithStyles, withStyles } from '@material-ui/core/styles';
import Table from '@material-ui/core/Table';
import TableBody from '@material-ui/core/TableBody';
import TableCell from '@material-ui/core/TableCell';
import TableHead from '@material-ui/core/TableHead';
import TableRow from '@material-ui/core/TableRow';
import IconLink from "@material-ui/icons/Link";
import { dotnetifyVM, IRouter } from "dotnetify";
import { RouteLink } from 'dotnetify/react';
import * as React from 'react';


const styles = (theme: Theme) => createStyles({
});

interface Props extends WithStyles<typeof styles> {
	vm: dotnetifyVM;
	data: MeterListRow[];
};

type MeterListRow = {
	MeterId: string;
	MeterState: string;
	Route: string;
};

class MeterListTableComponent extends React.Component<Props, any> {

	constructor(props) {
		super(props);
	}

	render() {
		const meters = this.props.data;

		return (
			<Table>
				<TableHead>
					<TableRow>
						<TableCell>ID</TableCell>
						<TableCell>State</TableCell>
						<TableCell>Detail</TableCell>
					</TableRow>
				</TableHead>
				<TableBody>
					{meters && meters.map(meter => (
						<TableRow key={meter.MeterId}>
							<TableCell>{meter.MeterId}</TableCell>
							<TableCell component="th" scope="row">
								{!meter ? "N/A" : meter.MeterState}
							</TableCell>
							<TableCell>
								<RouteLink vm={this.props.vm} route={meter.Route}>
									<Fab
										size="small"
									>
										<IconLink />
									</Fab>
								</RouteLink>
							</TableCell>
						</TableRow>
					))}
				</TableBody>
			</Table >
		);
	}
};
export default Object.assign(withStyles(styles)(MeterListTableComponent), { name: '' });